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
///     If no record is found for the specific location, falls back to a record whose
///     <c>entity_id</c> matches <c>Intacct:EntityId</c> (entity-level default).
///   </item>
///   <item>
///     <c>dutchie_field_config</c> — GL account mapping rows. Filtered to rows whose
///     <c>Rdutchielocationconfig</c> matches the location config's <c>RECORDNO</c>.
///     Supports <c>input_type</c> values: header, paymentsummary, taxratesummary,
///     categorysummary, customertypesummary.
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

    /// <summary>
    /// Loads one <see cref="ErpMappingConfig"/> per <c>dutchie_location_config</c> record.
    /// Uses three Intacct API calls regardless of location count:
    /// master config, all location configs, all field configs (grouped in memory).
    /// </summary>
    public async Task<IReadOnlyList<ErpMappingConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        var client = BuildClient();

        // ── 1. Master config ──────────────────────────────────────────────────
        var masterRows = await QueryObjectAsync(
            client,
            MasterConfigObject,
            ["RECORDNO", "RGLJOURNAL", "maximum_overshort", "is_live"],
            filter: null,
            cancellationToken).ConfigureAwait(false);

        var master = masterRows.Select(DutchieMasterConfigRow.FromXElement).FirstOrDefault();

        if (master is null)
            _logger.LogWarning("No {Object} record found. Using defaults for all locations.", MasterConfigObject);

        // ── 2. ALL location configs (no filter) ───────────────────────────────
        var locationRows = await QueryObjectAsync(
            client,
            LocationConfigObject,
            ["RECORDNO", "Rdutchiemasterconfig", "RLOC", "entity_id",
             "dutchie_location_key", "dutchie_integrator_key",
             "RCUSTOMER", "RDEPARTMENT", "RITEM"],
            filter: null,
            cancellationToken).ConfigureAwait(false);

        var locations = locationRows.Select(DutchieLocationConfigRow.FromXElement).ToList();

        if (locations.Count == 0)
        {
            _logger.LogWarning("No {Object} records found. No locations to sync.", LocationConfigObject);
            return [];
        }

        _logger.LogInformation("Found {Count} {Object} record(s) to sync.", locations.Count, LocationConfigObject);

        // ── 3. ALL field configs (no filter) — one round trip for all locations ─
        var allFieldRows = await QueryObjectAsync(
            client,
            FieldConfigObject,
            ["RECORDNO", "Rdutchielocationconfig", "input_type", "api_field", "credit_debit",
             "RGLACCOUNT", "RCUSTOMER", "RDEPARTMENT", "RITEM", "RCLASS", "amount", "entry_type"],
            filter: null,
            cancellationToken).ConfigureAwait(false);

        // Group field rows by the location config RECORDNO they belong to.
        var fieldsByLocation = allFieldRows
            .GroupBy(el => el.Element("Rdutchielocationconfig")?.Value?.Trim() ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());

        return locations.Select(loc =>
        {
            var locFields = fieldsByLocation.GetValueOrDefault(loc.RecordNo ?? string.Empty, []);
            _logger.LogDebug("Location {LocationId} ({RecordNo}): {Count} field config row(s).",
                loc.LocationId, loc.RecordNo, locFields.Count);
            return BuildErpMappingConfig(master, loc, locFields);
        }).ToList();
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
                "No {Object} record found. Using defaults (JournalSymbol=GJ, IsLive=true).", MasterConfigObject);
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

        // ── 2a. Entity-level fallback ─────────────────────────────────────────
        // If no location-specific row was found, look for a record whose entity_id matches
        // the configured EntityId. This allows a single "master" location config to serve
        // all locations within an entity.
        if (location is null && !string.IsNullOrWhiteSpace(_options.EntityId) && locationFilter is not null)
        {
            _logger.LogDebug(
                "No {Object} record found for location {LocationId}. " +
                "Attempting entity-level fallback for entity {EntityId}.",
                LocationConfigObject, _options.LocationId, _options.EntityId);

            var entityFilter  = new Filter("entity_id").SetEqualTo(_options.EntityId);
            var entityRows = await QueryObjectAsync(
                client,
                LocationConfigObject,
                ["RECORDNO", "Rdutchiemasterconfig", "RLOC", "entity_id",
                 "dutchie_location_key", "dutchie_integrator_key",
                 "RCUSTOMER", "RDEPARTMENT", "RITEM"],
                entityFilter,
                cancellationToken).ConfigureAwait(false);

            location = entityRows.Select(DutchieLocationConfigRow.FromXElement).FirstOrDefault();

            if (location is not null)
                _logger.LogInformation(
                    "Using entity-level location config (entity {EntityId}) for location {LocationId}.",
                    _options.EntityId, _options.LocationId);
        }

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
        IFilter? fieldFilter = location?.RecordNo is not null
            ? new Filter("Rdutchielocationconfig").SetEqualTo(location.RecordNo)
            : locationFilter is null ? null
              : throw new InvalidOperationException(
                  $"No {LocationConfigObject} record found for location '{_options.LocationId}'. " +
                  $"Ensure a dutchie_location_config record exists for this location (or an entity-level fallback).");

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
        var paymentMap      = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var taxRateMap      = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var categoryMap     = new Dictionary<string, SummaryLineConfig>(StringComparer.OrdinalIgnoreCase);
        var customerTypeMap = new Dictionary<string, SummaryLineConfig>(StringComparer.OrdinalIgnoreCase);

        string? cannabisSales    = null;
        string? nonCannabisSales = null;
        string? discount         = null;
        string? defaultTax       = null;
        string? tips             = null;
        string? fees             = null;
        string? roundingAccount  = null;

        foreach (var row in fieldRows)
        {
            var inputType = GetXValue(row, "input_type");
            var apiField  = GetXValue(row, "api_field");
            var glAccount = GetXValue(row, "RGLACCOUNT");

            if (string.IsNullOrEmpty(inputType) || string.IsNullOrEmpty(glAccount))
            {
                _logger.LogWarning(
                    "Skipping {Object} row {Id}: missing input_type or RGLACCOUNT.",
                    FieldConfigObject, GetXValue(row, "RECORDNO") ?? "?");
                continue;
            }

            switch (inputType.ToLowerInvariant())
            {
                case "header":
                    if (string.IsNullOrEmpty(apiField))
                    {
                        _logger.LogWarning(
                            "Skipping header {Object} row {Id}: missing api_field.",
                            FieldConfigObject, GetXValue(row, "RECORDNO") ?? "?");
                        break;
                    }
                    switch (apiField.ToLowerInvariant())
                    {
                        case "cannabissales":                             cannabisSales    = glAccount; break;
                        case "noncannabissales":                          nonCannabisSales = glAccount; break;
                        case "totaldiscount" or "discount":               discount         = glAccount; break;
                        case "defaulttax" or "tax":                       defaultTax       = glAccount; break;
                        case "tips":                                       tips             = glAccount; break;
                        case "fees" or "donations" or "feesanddonations": fees             = glAccount; break;
                        case "rounding" or "finaladjustment":             roundingAccount  = glAccount; break;
                        default:
                            _logger.LogDebug("Unrecognised header api_field '{Field}' — skipped.", apiField);
                            break;
                    }
                    break;

                case "paymentsummary":
                    if (!string.IsNullOrEmpty(apiField))
                        paymentMap[apiField] = glAccount;
                    break;

                case "taxratesummary":
                    if (!string.IsNullOrEmpty(apiField))
                        taxRateMap[apiField] = glAccount;
                    break;

                case "categorysummary":
                    // api_field = category name; empty string = default fallback row.
                    categoryMap[apiField ?? string.Empty] = BuildSummaryLineConfig(row, glAccount, isCredit: true);
                    break;

                case "customertypesummary":
                    // api_field = customer type name; empty string = default fallback row.
                    customerTypeMap[apiField ?? string.Empty] = BuildSummaryLineConfig(row, glAccount, isCredit: true);
                    break;

                default:
                    _logger.LogDebug("Unrecognised input_type '{InputType}' — skipped.", inputType);
                    break;
            }
        }

        return new ErpMappingConfig
        {
            JournalSymbol           = master?.GlJournalSymbol      ?? "GJ",
            IsLive                  = master?.IsLive                ?? true,
            MaximumOverShort        = master?.MaximumOverShort      ?? 1m,
            LocationConfigRecordNo  = location?.RecordNo,
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
            RoundingAccount         = roundingAccount,
            PaymentTypeAccountMap   = paymentMap,
            TaxRateAccountMap       = taxRateMap,
            CategoryAccountMap      = categoryMap,
            CustomerTypeAccountMap  = customerTypeMap,
        };
    }

    /// <summary>
    /// Builds a <see cref="SummaryLineConfig"/> from a <c>dutchie_field_config</c> row,
    /// reading optional per-entry dimension overrides and the amount selector.
    /// </summary>
    private static SummaryLineConfig BuildSummaryLineConfig(XElement row, string glAccount, bool isCredit)
    {
        // credit_debit field overrides the default (e.g. "Debit" for contra-revenue entries).
        var creditDebitStr = GetXValue(row, "credit_debit");
        if (!string.IsNullOrEmpty(creditDebitStr))
            isCredit = !creditDebitStr.Equals("Debit", StringComparison.OrdinalIgnoreCase);

        var amountStr = GetXValue(row, "amount");
        var amountSelector = amountStr?.ToLowerInvariant() switch
        {
            "gross"    => AmountSelector.Gross,
            "discount" => AmountSelector.Discount,
            "cost"     => AmountSelector.Cost,
            _          => AmountSelector.Net,
        };

        return new SummaryLineConfig
        {
            Account          = glAccount,
            AmountSelector   = amountSelector,
            IsCredit         = isCredit,
            DepartmentId     = GetXValue(row, "RDEPARTMENT"),
            ClassId          = GetXValue(row, "RCLASS"),
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
