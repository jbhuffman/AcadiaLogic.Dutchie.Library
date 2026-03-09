using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using AcadiaLogic.Dutchie.Intacct.Configuration;
using Intacct.SDK;
using Intacct.SDK.Exceptions;
using Intacct.SDK.Functions.AccountsReceivable;
using Intacct.SDK.Functions.GeneralLedger;
using Intacct.SDK.Xml;
using Intacct.SDK.Xml.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie.Intacct.Connectors;

/// <summary>
/// Sage Intacct implementation of <see cref="IErpConnector"/>.
/// Posts journal entries (closing report) and AR invoices (sales transactions) via the Intacct SDK.
/// </summary>
public sealed class IntacctErpConnector : IErpConnector
{
    public string ConnectorName => "SageIntacct";

    private readonly IntacctOptions _options;
    private readonly ILogger<IntacctErpConnector> _logger;

    public IntacctErpConnector(IOptions<IntacctOptions> options, ILogger<IntacctErpConnector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> PostJournalEntryAsync(JournalEntryPayload entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Posting journal entry {Ref} to Sage Intacct", entry.ReferenceNumber);

        if (!entry.Lines.Any())
            throw new InvalidOperationException("Journal entry has no lines.");

        var je = new JournalEntryCreate
        {
            ControlId      = entry.ReferenceNumber,
            JournalSymbol  = entry.JournalSymbol,
            PostingDate    = entry.Date.ToDateTime(TimeOnly.MinValue),
            ReferenceNumber = entry.ReferenceNumber,
            Description    = entry.Description,
            SourceEntityId = entry.LocationId,
        };

        // Draft mode: hold for manual review; Live mode: post immediately.
        // The Intacct SDK exposes this via the Action property on AbstractGlBatch.
        if (entry.PostAsDraft)
            je.Action = "Draft";

        foreach (var line in entry.Lines)
        {
            var jel = new JournalEntryLineCreate();
            jel.GlAccountNumber   = line.AccountNumber;
            jel.TransactionAmount = line.Amount;
            jel.Memo              = line.Memo;
            jel.LocationId        = line.LocationId ?? entry.LocationId;
            jel.DepartmentId      = line.DepartmentId;
            jel.ClassId           = line.ClassId;
            je.Lines.Add(jel);
        }

        var client = BuildClient();
        var response = await client.Execute(je, new RequestConfig()).ConfigureAwait(false);
        var result = GetFirstResult(response);
        EnsureSuccess(result, "journal entry", entry.ReferenceNumber);

        _logger.LogInformation("Journal entry posted. Intacct key: {Key}", result.Key);
        return result.Key;
    }

    public async Task<string> PostSalesTransactionAsync(SalesTransactionPayload transaction, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Posting AR invoice for Dutchie transaction {Id} to Sage Intacct", transaction.DutchieTransactionId);

        var invoice = new InvoiceCreate
        {
            ControlId = $"DUTCHIE-TXN-{transaction.DutchieTransactionId}"
        };

        invoice.CustomerId = transaction.ErpCustomerId;
        invoice.TransactionDate = transaction.TransactionDate.DateTime;
        invoice.InvoiceNumber = transaction.InvoiceNumber ?? $"DTCH-{transaction.DutchieTransactionId}";
        invoice.ReferenceNumber = transaction.DutchieTransactionId.ToString();
        invoice.Description = $"Dutchie POS — {transaction.OrderType ?? "Sale"} — {transaction.TransactionDate:yyyy-MM-dd}";
        invoice.ExternalId = transaction.DutchieTransactionId.ToString();

        foreach (var line in transaction.LineItems)
        {
            var il = new InvoiceLineCreate();
            il.ItemId = line.Sku ?? line.DutchieProductId.ToString();
            il.Memo = line.ProductName;
            il.TransactionAmount = line.UnitPrice * line.Quantity - line.TotalDiscount;
            il.LocationId = transaction.LocationId;
            invoice.Lines.Add(il);
        }

        var client = BuildClient();
        var response = await client.Execute(invoice, new RequestConfig()).ConfigureAwait(false);
        var result = GetFirstResult(response);
        EnsureSuccess(result, "AR invoice", invoice.InvoiceNumber!);

        _logger.LogInformation("AR invoice posted. Intacct key: {Key}", result.Key);
        return result.Key;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private OnlineClient BuildClient()
    {
        var config = new ClientConfig
        {
            CompanyId = _options.CompanyId,
            UserId = _options.UserId,
            UserPassword = _options.UserPassword,
            SenderId = _options.SenderId,
            SenderPassword = _options.SenderPassword,
            EntityId = _options.EntityId
        };
        return new OnlineClient(config);
    }

    private static Result GetFirstResult(OnlineResponse response)
    {
        var results = response.Results;
        if (results == null || results.Count == 0)
            throw new IntacctException("Intacct API returned no results.");
        return results[0];
    }

    private static void EnsureSuccess(Result result, string entityType, string reference)
    {
        if (result.Status == "success") return;

        var errors = result.Errors != null
            ? string.Join("; ", result.Errors)
            : "(no error detail)";

        throw new ResponseException(
            $"Intacct rejected {entityType} '{reference}': {errors}",
            result.Errors ?? []);
    }

}
