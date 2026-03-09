using AcadiaLogic.Dutchie.Clients;
using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using AcadiaLogic.Dutchie.Models.Reporting;
using Microsoft.Extensions.Logging;

namespace AcadiaLogic.Dutchie.Integration.Pipeline;

/// <summary>
/// Orchestrates: pull transactions from Dutchie → map to SalesTransactionPayload → post to ERP.
/// Uses last-modified watermark for incremental sync.
/// </summary>
public sealed class TransactionSyncPipeline
{
    public const string JobName = "Transactions";

    private readonly IReportingClient _reporting;
    private readonly IErpConnector _erp;
    private readonly IErpConfigProvider _config;
    private readonly ISyncStateStore _state;
    private readonly ILogger<TransactionSyncPipeline> _logger;

    public TransactionSyncPipeline(
        IReportingClient reporting,
        IErpConnector erp,
        IErpConfigProvider config,
        ISyncStateStore state,
        ILogger<TransactionSyncPipeline> logger)
    {
        _reporting = reporting;
        _erp = erp;
        _config = config;
        _state = state;
        _logger = logger;
    }

    /// <summary>
    /// Pulls transactions modified since the last successful sync and posts each to the ERP.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var mappingConfig = await _config.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var lastSync = await _state.GetLastSyncTimeAsync(JobName, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        _logger.LogInformation("Transaction sync: from {From} to {To}", lastSync, now);

        var transactions = await _reporting.GetTransactionsAsync(
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
        var failed = 0;

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
                failed++;
            }
        }

        _logger.LogInformation("Transaction sync complete. Posted: {Posted}, Failed: {Failed}", posted, failed);

        if (failed == 0)
            await _state.SetLastSyncTimeAsync(JobName, now, cancellationToken).ConfigureAwait(false);
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
