using AcadiaLogic.Dutchie.Clients;
using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using AcadiaLogic.Dutchie.Models.Reporting;
using Microsoft.Extensions.Logging;

namespace AcadiaLogic.Dutchie.Integration.Pipeline;

/// <summary>
/// Orchestrates: pull transactions from Dutchie → map to SalesTransactionPayload → post to ERP.
/// Uses a per-location last-modified watermark for incremental sync.
/// The caller (worker) provides the per-location <see cref="IReportingClient"/> and
/// <see cref="ErpMappingConfig"/> so a single pipeline instance can serve all locations.
/// </summary>
public sealed class TransactionSyncPipeline
{
    public const string JobName = "Transactions";

    private readonly IErpConnector _erp;
    private readonly ISyncStateStore _state;
    private readonly ILogger<TransactionSyncPipeline> _logger;

    public TransactionSyncPipeline(
        IErpConnector erp,
        ISyncStateStore state,
        ILogger<TransactionSyncPipeline> logger)
    {
        _erp    = erp;
        _state  = state;
        _logger = logger;
    }

    /// <summary>
    /// Pulls transactions modified since the last successful sync for the given location
    /// and posts each to the ERP.
    /// Writes a <c>dutchie_process_log</c> record to Intacct on both success and failure.
    /// Individual transaction failures are counted and included in the log but do not abort the run.
    /// </summary>
    /// <param name="reporting">Per-location Dutchie API client (credentials already set by caller).</param>
    /// <param name="mappingConfig">GL mapping config for this location.</param>
    public async Task RunAsync(
        IReportingClient reporting,
        ErpMappingConfig mappingConfig,
        CancellationToken cancellationToken = default)
    {
        // Per-location watermark key so each location maintains its own cursor.
        var stateKey = mappingConfig.LocationId is null
            ? JobName
            : $"{JobName}-{mappingConfig.LocationId}";

        var lastSync = await _state.GetLastSyncTimeAsync(stateKey, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        _logger.LogInformation("Transaction sync [{Location}]: from {From} to {To}",
            mappingConfig.LocationId ?? "global", lastSync, now);

        try
        {
            var transactions = await reporting.GetTransactionsAsync(
                new TransactionQueryRequest
                {
                    FromLastModifiedDateUtc = lastSync,
                    ToLastModifiedDateUtc = now,
                    IncludeDetail = true,
                    IncludeTaxes = true,
                    IncludeFeesAndDonations = true
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Retrieved {Count} transactions", transactions.Count);

            var posted = 0;
            var failedIds = new List<string>();

            foreach (var tx in transactions.Where(t => !t.IsVoid && !t.IsReturn))
            {
                try
                {
                    var payload = MapTransaction(tx, mappingConfig);
                    var key = await _erp.PostSalesTransactionAsync(payload, cancellationToken).ConfigureAwait(false);
                    _logger.LogDebug("Transaction {Id} posted. ERP key: {Key}", tx.TransactionId, key);
                    posted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to post transaction {Id}", tx.TransactionId);
                    failedIds.Add(tx.TransactionId.ToString());
                }
            }

            _logger.LogInformation("Transaction sync complete [{Location}]. Posted: {Posted}, Failed: {Failed}",
                mappingConfig.LocationId ?? "global", posted, failedIds.Count);

            if (failedIds.Count == 0)
                await _state.SetLastSyncTimeAsync(stateKey, now, cancellationToken).ConfigureAwait(false);

            // Write process log — status is failed when any transactions could not be posted
            // so that operators can identify the run and reprocess the failed IDs.
            var hasFailures = failedIds.Count > 0;
            await _erp.WriteProcessLogAsync(new ProcessLogEntry
            {
                JobName                = JobName,
                Status                 = hasFailures ? ProcessLogEntry.Statuses.Failed : ProcessLogEntry.Statuses.Complete,
                RecordsProcessed       = posted,
                SummarizedErrors       = hasFailures
                    ? $"{failedIds.Count} transaction(s) failed to post: {string.Join(", ", failedIds)}"
                    : null,
                LocationConfigRecordNo = mappingConfig.LocationConfigRecordNo,
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction sync failed");

            await _erp.WriteProcessLogAsync(new ProcessLogEntry
            {
                JobName                = JobName,
                Status                 = ProcessLogEntry.Statuses.Failed,
                SummarizedErrors       = ex.Message,
                RawErrors              = ex.ToString(),
                LocationConfigRecordNo = mappingConfig.LocationConfigRecordNo,
            }, CancellationToken.None).ConfigureAwait(false);

            throw;
        }
    }

    private static SalesTransactionPayload MapTransaction(Transaction tx, ErpMappingConfig cfg)
    {
        var lines = (tx.Items ?? []).Select(item => new SalesLineItem
        {
            DutchieProductId = item.ProductId,
            Sku = null,
            Quantity = (decimal)item.Quantity,
            UnitPrice = (decimal)item.UnitPrice,
            TotalDiscount = (decimal)item.TotalDiscount,
            TaxAmount = (decimal)(item.Taxes?.Sum(t => t.Amount) ?? 0)
        }).ToList();

        var payments = BuildPayments(tx, cfg);

        return new SalesTransactionPayload
        {
            DutchieTransactionId = tx.TransactionId,
            TransactionDate = tx.TransactionDate,
            ErpCustomerId = cfg.DefaultCustomerId,
            InvoiceNumber = tx.InvoiceNumber,
            OrderType = tx.OrderType,
            OrderSource = tx.OrderSource,
            IsMedical = tx.IsMedical,
            LineItems = lines,
            Payments = payments,
            Subtotal = (decimal)tx.Subtotal,
            TotalDiscount = (decimal)tx.TotalDiscount,
            TotalTax = (decimal)tx.Tax,
            Total = (decimal)tx.Total,
            LocationId = cfg.LocationId
        };
    }

    private static IReadOnlyList<SalesPayment> BuildPayments(Transaction tx, ErpMappingConfig cfg)
    {
        var payments = new List<SalesPayment>();

        void Add(string label, double? amount)
        {
            if (amount is > 0)
                payments.Add(new SalesPayment { PaymentMethod = label, Amount = (decimal)amount.Value });
        }

        Add("Cash", tx.CashPaid);
        Add("Debit", tx.DebitPaid);
        Add("Credit", tx.CreditPaid);
        Add("Gift", tx.GiftPaid);
        Add("Check", tx.CheckPaid);
        Add("MMAP", tx.MmapPaid);
        Add(tx.ElectronicPaymentMethod ?? "Electronic", tx.ElectronicPaid);

        foreach (var mp in tx.ManualPayments ?? [])
            Add(mp.ManualPaymentProcessorName ?? "Manual", mp.ManualPaid);

        foreach (var ip in tx.IntegratedPayments ?? [])
            Add(ip.IntegrationType ?? "Integrated", ip.IntegratedPaid);

        return payments;
    }
}
