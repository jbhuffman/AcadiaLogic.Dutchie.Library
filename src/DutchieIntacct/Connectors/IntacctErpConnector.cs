using Dutchie.Integration.Abstractions;
using Dutchie.Integration.Models;
using Dutchie.Intacct.Configuration;
using Intacct.SDK;
using Intacct.SDK.Exceptions;
using Intacct.SDK.Functions;
using Intacct.SDK.Functions.AccountsReceivable;
using Intacct.SDK.Functions.GeneralLedger;
using Intacct.SDK.Xml;
using Intacct.SDK.Xml.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dutchie.Intacct.Connectors;

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

    /// <inheritdoc/>
    /// <remarks>
    /// All exceptions are caught and logged as warnings. A process-log write failure must never
    /// propagate to the calling pipeline or mask the original sync exception.
    /// </remarks>
    public async Task WriteProcessLogAsync(ProcessLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var fn = new ProcessLogCreateFunction(entry);
            var client = BuildClient();
            var response = await client.Execute(fn, new RequestConfig()).ConfigureAwait(false);
            var result = response.Results?.FirstOrDefault();

            if (result?.Status != "success")
            {
                var errors = result?.Errors != null ? string.Join("; ", result.Errors) : "(no detail)";
                _logger.LogWarning(
                    "Intacct rejected dutchie_process_log create for job {Job}: {Errors}",
                    entry.JobName, errors);
            }
            else
            {
                _logger.LogDebug(
                    "Process log written for job {Job} (status={Status}, records={Records}). Intacct key: {Key}",
                    entry.JobName, entry.Status, entry.RecordsProcessed, result.Key);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: swallow and warn — never let log write failure affect the worker.
            _logger.LogWarning(ex,
                "Failed to write dutchie_process_log to Intacct for job {Job}. " +
                "The sync run itself was not affected.", entry.JobName);
        }
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

    // ── Platform App: process log create ─────────────────────────────────────

    /// <summary>
    /// Intacct SDK function that creates a <c>dutchie_process_log</c> custom object record.
    /// Writes the Intacct XML Gateway <c>create</c> operation directly since there is no
    /// typed SDK class for Platform App custom objects.
    /// </summary>
    private sealed class ProcessLogCreateFunction : AbstractFunction
    {
        private readonly ProcessLogEntry _entry;

        public ProcessLogCreateFunction(ProcessLogEntry entry)
            : base($"plog-{Guid.NewGuid():N}")
        {
            _entry = entry;
        }

        public override void WriteXml(ref IaXmlWriter xml)
        {
            xml.WriteStartElement("function");
            xml.WriteAttribute("controlid", ControlId);

            xml.WriteStartElement("create");
            xml.WriteStartElement("dutchie_process_log");

            xml.WriteElementString("job_name", _entry.JobName);
            xml.WriteElementString("status", _entry.Status);
            xml.WriteElementString("records_processed", _entry.RecordsProcessed.ToString());

            if (!string.IsNullOrEmpty(_entry.RawErrors))
            {
                // Intacct text fields have a practical limit; truncate to avoid rejection.
                var raw = _entry.RawErrors.Length > 4000
                    ? _entry.RawErrors[..4000]
                    : _entry.RawErrors;
                xml.WriteElementString("raw_errors", raw);
            }

            if (!string.IsNullOrEmpty(_entry.SummarizedErrors))
                xml.WriteElementString("summarized_readable_errors", _entry.SummarizedErrors);

            if (!string.IsNullOrEmpty(_entry.LocationConfigRecordNo))
                xml.WriteElementString("Rdutchielocationconfig", _entry.LocationConfigRecordNo);

            xml.WriteEndElement(); // dutchie_process_log
            xml.WriteEndElement(); // create
            xml.WriteEndElement(); // function
        }
    }
}
