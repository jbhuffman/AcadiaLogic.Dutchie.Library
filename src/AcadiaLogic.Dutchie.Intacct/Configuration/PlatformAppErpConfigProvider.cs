using System.Xml.Linq;
using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using Intacct.SDK;
using Intacct.SDK.Functions.Common.NewQuery;
using Intacct.SDK.Functions.Common.NewQuery.QueryFilter;
using Intacct.SDK.Functions.Common.NewQuery.QuerySelect;
using Intacct.SDK.Xml;
using Intacct.SDK.Xml.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie.Intacct.Configuration;

/// <summary>
/// Loads <see cref="ErpMappingConfig"/> from the Dutchie Integration Platform Application
/// custom objects deployed in Intacct.
/// Activate by calling <c>.UsePlatformAppConfig()</c> after <c>.AddIntacctConnector()</c>.
/// </summary>
/// <remarks>
/// <b>Query sequence (three objects):</b>
/// <list type="number">
///   <item>
///     <c>dutchie_master_config</c> — company-level settings (GL journal, over/short tolerance,
///     live flag). Only the first record is used; there is typically one per company.
///   </item>
///   <item>
///     <c>dutchie_location_config</c> — per-location settings (Dutchie API credentials, default
///     customer/department/item). Filtered by <c>Intacct:LocationId</c> when set.
///   </item>
///   <item>
///     <c>dutchie_field_config</c> — GL account mapping rows. Filtered to rows whose
///     <c>Rdutchielocationconfig</c> matches the location config's <c>RECORDNO</c>.
///   </item>
/// </list>
/// </remarks>
public sealed class PlatformAppErpConfigProvider : IErpConfigProvider
{
    private const string MasterConfigObject   = "dutchie_master_config";
    private const string LocationConfigObject = "dutchie_location_config";
    private const string FieldConfigObject    = "dutchie_field_config";

    private readonly IntacctOptions _options;
    private readonly ILogger<PlatformAppErpConfigProvider> _logger;

    public PlatformAppErpConfigProvider(
        IOptions<IntacctOptions> options,
        ILogger<PlatformAppErpConfigProvider> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public async Task<ErpMappingConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var client = BuildClient();

        // ── 1. Query master config (company-level) ────────────────────────────
        var masterRows = await QueryObjectAsync(
            client,
            MasterConfigObject,
            ["RECORDNO", "RGLJOURNAL", "maximum_overshort", "is_live"],
            filter: null,
            cancellationToken).ConfigureAwait(false);

        var master = masterRows.Select(DutchieMasterConfigRow.FromXElement).FirstOrDefault();

        if (master is null)
            _logger.LogWarning(
                "No {Object} record found. Using defaults (JournalSymbol=GJ).", MasterConfigObject);
        else
            _logger.LogDebug(
                "Master config: Journal={Journal}, Live={Live}, MaxOverShort={MaxOverShort}",
                master.GlJournalSymbol, master.IsLive, master.MaximumOverShort);

        // ── 2. Query location config ──────────────────────────────────────────
        IFilter? locationFilter = string.IsNullOrWhiteSpace(_options.LocationId)
            ? null
            : new Filter("RLOC").SetEqualTo(_options.LocationId);

        if (locationFilter is null)
            _logger.LogWarning(
                "Intacct:LocationId is not set. Loading {Object} for ALL locations. " +
                "Set Intacct:LocationId to scope config to a single location.",
                LocationConfigObject);
        else
            _logger.LogDebug("Loading {Object} for location {LocationId}.",
                LocationConfigObject, _options.LocationId);

        var locationRows = await QueryObjectAsync(
            client,
            LocationConfigObject,
            ["RECORDNO", "Rdutchiemasterconfig", "RLOC", "entity_id",
             "dutchie_location_key", "dutchie_integrator_key",
             "RCUSTOMER", "RDEPARTMENT", "RITEM"],
            locationFilter,
            cancellationToken).ConfigureAwait(false);

        var location = locationRows.Select(DutchieLocationConfigRow.FromXElement).FirstOrDefault();

        if (location is null)
        {
            _logger.LogWarning(
                "No {Object} record found{Filter}. Default customer will be 'WALKIN'.",
                LocationConfigObject,
                _options.LocationId is null ? "" : $" for location {_options.LocationId}");
        }
        else
        {
            _logger.LogDebug(
                "Location config: Location={Location}, HasDutchieCreds={HasCreds}, DefaultCustomer={Customer}",
                location.LocationId,
                !string.IsNullOrWhiteSpace(location.DutchieLocationKey),
                location.DefaultCustomerId);
        }

        // ── 3. Query field config rows ────────────────────────────────────────
        // Field configs are scoped to a location config via the Rdutchielocationconfig relationship.
        IFilter? fieldFilter = location?.RecordNo is not null
            ? new Filter("Rdutchielocationconfig").SetEqualTo(location.RecordNo)
            : locationFilter is null ? null
              : throw new InvalidOperationException(
                  $"No {LocationConfigObject} record found for location '{_options.LocationId}'. " +
                  $"Ensure a dutchie_location_config record exists for this location.");

        var fieldRows = await QueryObjectAsync(
            client,
            FieldConfigObject,
            ["RECORDNO", "Rdutchielocationconfig", "input_type", "api_field", "credit_debit",
             "RGLACCOUNT", "RCUSTOMER", "RDEPARTMENT", "RITEM", "RCLASS", "amount", "entry_type"],
            fieldFilter,
            cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Loaded {Count} {Object} rows.", fieldRows.Count, FieldConfigObject);

        return BuildErpMappingConfig(master, location, fieldRows);
    }

    // ── Config builder ────────────────────────────────────────────────────────

    private ErpMappingConfig BuildErpMappingConfig(
        DutchieMasterConfigRow? master,
        DutchieLocationConfigRow? location,
        List<XElement> fieldRows)
    {
        var paymentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var taxRateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? cannabisSales    = null;
        string? nonCannabisSales = null;
        string? discount         = null;
        string? defaultTax       = null;
        string? tips             = null;
        string? fees             = null;

        foreach (var row in fieldRows)
        {
            var inputType = GetXValue(row, "input_type");
            var apiField  = GetXValue(row, "api_field");
            var glAccount = GetXValue(row, "RGLACCOUNT");

            if (string.IsNullOrEmpty(inputType) || string.IsNullOrEmpty(apiField) || string.IsNullOrEmpty(glAccount))
            {
                _logger.LogWarning(
                    "Skipping {Object} row {Id}: missing input_type, api_field, or RGLACCOUNT.",
                    FieldConfigObject, GetXValue(row, "RECORDNO") ?? "?");
                continue;
            }

            switch (inputType.ToLowerInvariant())
            {
                case "header":
                    switch (apiField.ToLowerInvariant())
                    {
                        case "cannabissales":                        cannabisSales    = glAccount; break;
                        case "noncannabissales":                     nonCannabisSales = glAccount; break;
                        case "totaldiscount" or "discount":          discount         = glAccount; break;
                        case "defaulttax" or "tax":                  defaultTax       = glAccount; break;
                        case "tips":                                  tips             = glAccount; break;
                        case "fees" or "donations" or "feesanddonations": fees         = glAccount; break;
                        default:
                            _logger.LogDebug("Unrecognised header api_field '{Field}' — skipped.", apiField);
                            break;
                    }
                    break;

                case "paymentsummary":
                    paymentMap[apiField] = glAccount;
                    break;

                case "taxratesummary":
                    taxRateMap[apiField] = glAccount;
                    break;

                case "categorysummary":
                case "customertypesummary":
                    // Reserved for future pipeline use.
                    break;

                default:
                    _logger.LogDebug("Unrecognised input_type '{InputType}' — skipped.", inputType);
                    break;
            }
        }

        return new ErpMappingConfig
        {
            JournalSymbol           = master?.GlJournalSymbol      ?? "GJ",
            LocationId              = location?.LocationId,
            DepartmentId            = location?.DefaultDepartmentId,
            DefaultItemId           = location?.DefaultItemId,
            DefaultCustomerId       = location?.DefaultCustomerId   ?? "WALKIN",
            DutchieLocationKey      = location?.DutchieLocationKey,
            DutchieIntegratorKey    = location?.DutchieIntegratorKey,
            CannabisSalesAccount    = cannabisSales    ?? string.Empty,
            NonCannabisSalesAccount = nonCannabisSales ?? string.Empty,
            DiscountAccount         = discount         ?? string.Empty,
            DefaultTaxAccount       = defaultTax       ?? string.Empty,
            TipsAccount             = tips,
            FeesAccount             = fees,
            PaymentTypeAccountMap   = paymentMap,
            TaxRateAccountMap       = taxRateMap,
        };
    }

    // ── SDK helpers ───────────────────────────────────────────────────────────

    private async Task<List<XElement>> QueryObjectAsync(
        OnlineClient client,
        string objectName,
        string[] fields,
        IFilter? filter,
        CancellationToken cancellationToken)
    {
        var selectFields = new SelectBuilder().Fields(fields).GetFields();

        var query = new QueryFunction($"{objectName}-load")
        {
            FromObject   = objectName,
            SelectFields = selectFields,
            Filter       = filter,
            PageSize     = 1000,
        };

        OnlineResponse response;
        try
        {
            response = await client.Execute(query, new RequestConfig()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to query Intacct Platform App object '{objectName}'. " +
                $"Ensure the Dutchie Integration Platform App is deployed and credentials are correct.", ex);
        }

        var result = response.Results?.FirstOrDefault();
        if (result is null)
            throw new InvalidOperationException($"Intacct returned no result for query on '{objectName}'.");

        if (result.Status != "success")
        {
            var errors = result.Errors != null ? string.Join("; ", result.Errors) : "(no detail)";
            throw new InvalidOperationException($"Intacct query on '{objectName}' failed: {errors}");
        }

        return result.Data ?? [];
    }

    private static string? GetXValue(XElement? element, string childName)
    {
        if (element is null) return null;
        var value = element.Element(childName)?.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private OnlineClient BuildClient()
    {
        var config = new ClientConfig
        {
            CompanyId      = _options.CompanyId,
            UserId         = _options.UserId,
            UserPassword   = _options.UserPassword,
            SenderId       = _options.SenderId,
            SenderPassword = _options.SenderPassword,
            EntityId       = _options.EntityId,
        };
        return new OnlineClient(config);
    }
}
