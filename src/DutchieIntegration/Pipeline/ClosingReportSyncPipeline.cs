using Dutchie.Clients;
using Dutchie.Integration.Abstractions;
using Dutchie.Integration.Models;
using Microsoft.Extensions.Logging;

namespace Dutchie.Integration.Pipeline;

/// <summary>
/// Orchestrates: pull closing report from Dutchie → map to JournalEntryPayload → post to ERP.
/// The caller (worker) provides the per-location <see cref="IReportingClient"/> and
/// <see cref="ErpMappingConfig"/> so a single pipeline instance can serve all locations.
/// </summary>
public sealed class ClosingReportSyncPipeline
{
    public const string JobName = "ClosingReport";

    private readonly IErpConnector _erp;
    private readonly ISyncStateStore _state;
    private readonly ILogger<ClosingReportSyncPipeline> _logger;

    public ClosingReportSyncPipeline(
        IErpConnector erp,
        ISyncStateStore state,
        ILogger<ClosingReportSyncPipeline> logger)
    {
        _erp    = erp;
        _state  = state;
        _logger = logger;
    }

    /// <summary>
    /// Syncs a specific date range for the given location.
    /// Writes a <c>dutchie_process_log</c> record to Intacct on both success and failure.
    /// </summary>
    /// <param name="reporting">Per-location Dutchie API client (credentials already set by caller).</param>
    /// <param name="mappingConfig">GL mapping config for this location.</param>
    public async Task RunAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        IReportingClient reporting,
        ErpMappingConfig mappingConfig,
        CancellationToken cancellationToken = default)
    {
        // Use a per-location state key so multiple locations don't share a single watermark.
        var stateKey = mappingConfig.LocationId is null
            ? JobName
            : $"{JobName}-{mappingConfig.LocationId}";

        _logger.LogInformation("Closing report sync [{Location}]: {From} → {To}",
            mappingConfig.LocationId ?? "global", from, to);

        try
        {
            var report = await reporting.GetClosingReportAsync(from, to, cancellationToken).ConfigureAwait(false);
            var entry = BuildJournalEntry(report, from, to, mappingConfig);

            if (entry.Lines.Count == 0)
            {
                _logger.LogWarning(
                    "Closing report [{Location}] for {From}–{To} produced no GL lines. Nothing posted.",
                    mappingConfig.LocationId ?? "global", from, to);
                await _erp.WriteProcessLogAsync(new ProcessLogEntry
                {
                    JobName                = JobName,
                    Status                 = ProcessLogEntry.Statuses.Complete,
                    LocationConfigRecordNo = mappingConfig.LocationConfigRecordNo,
                }, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var key = await _erp.PostJournalEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Journal entry posted [{Location}] ({Mode}). ERP key: {Key}",
                mappingConfig.LocationId ?? "global", entry.PostAsDraft ? "Draft" : "Live", key);

            await _state.SetLastSyncTimeAsync(stateKey, to, cancellationToken).ConfigureAwait(false);

            await _erp.WriteProcessLogAsync(new ProcessLogEntry
            {
                JobName                = JobName,
                Status                 = ProcessLogEntry.Statuses.Complete,
                RecordsProcessed       = 1,
                LocationConfigRecordNo = mappingConfig.LocationConfigRecordNo,
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Closing report sync failed [{Location}] for {From}–{To}",
                mappingConfig.LocationId ?? "global", from, to);

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

    private JournalEntryPayload BuildJournalEntry(
        global::Dutchie.Models.Reporting.ClosingReport report,
        DateTimeOffset from,
        DateTimeOffset to,
        ErpMappingConfig cfg)
    {
        var lines = new List<JournalEntryLine>();
        var date = DateOnly.FromDateTime(to.LocalDateTime.Date);

        // ── Debit: each payment method received ──────────────────────────────
        foreach (var payment in report.PaymentSummary ?? [])
        {
            if (payment.TotalPaid == 0) continue;
            var account = cfg.PaymentTypeAccountMap.TryGetValue(payment.PaymentType ?? string.Empty, out var acc)
                ? acc
                : cfg.PaymentTypeAccountMap.GetValueOrDefault("Default", string.Empty);

            if (string.IsNullOrEmpty(account)) continue;

            lines.Add(new JournalEntryLine
            {
                AccountNumber = account,
                Amount        = (decimal)payment.TotalPaid,   // debit
                Memo          = $"{payment.PaymentType} receipts",
                LocationId    = cfg.LocationId,
                DepartmentId  = cfg.DepartmentId,
            });
        }

        // ── Credit: cannabis sales ────────────────────────────────────────────
        if (report.CannabisSales is > 0 && !string.IsNullOrEmpty(cfg.CannabisSalesAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.CannabisSalesAccount,
                Amount        = -(decimal)report.CannabisSales.Value,   // credit
                Memo          = "Cannabis net sales",
                LocationId    = cfg.LocationId,
                DepartmentId  = cfg.DepartmentId,
            });
        }

        // ── Credit: non-cannabis sales ────────────────────────────────────────
        if (report.NonCannabisSales is > 0 && !string.IsNullOrEmpty(cfg.NonCannabisSalesAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.NonCannabisSalesAccount,
                Amount        = -(decimal)report.NonCannabisSales.Value,    // credit
                Memo          = "Non-cannabis net sales",
                LocationId    = cfg.LocationId,
                DepartmentId  = cfg.DepartmentId,
            });
        }

        // ── Debit: discounts given ────────────────────────────────────────────
        if (report.Discount is > 0 && !string.IsNullOrEmpty(cfg.DiscountAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.DiscountAccount,
                Amount        = (decimal)report.Discount.Value,    // debit (contra-revenue)
                Memo          = "Sales discounts",
                LocationId    = cfg.LocationId,
                DepartmentId  = cfg.DepartmentId,
            });
        }

        // ── Credit: taxes by rate ─────────────────────────────────────────────
        foreach (var tax in report.TaxSummary ?? [])
        {
            if (tax.TotalTax == 0) continue;
            var account = cfg.TaxRateAccountMap.TryGetValue(tax.TaxRate ?? string.Empty, out var acc)
                ? acc
                : cfg.DefaultTaxAccount;

            if (string.IsNullOrEmpty(account)) continue;

            lines.Add(new JournalEntryLine
            {
                AccountNumber = account,
                Amount        = -(decimal)tax.TotalTax,     // credit (liability)
                Memo          = $"Tax: {tax.TaxRate}",
                LocationId    = cfg.LocationId,
                DepartmentId  = cfg.DepartmentId,
            });
        }

        // ── Credit: tips ──────────────────────────────────────────────────────
        if (report.TotalTips is > 0 && !string.IsNullOrEmpty(cfg.TipsAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.TipsAccount,
                Amount        = -(decimal)report.TotalTips.Value,   // credit
                Memo          = "Tips",
                LocationId    = cfg.LocationId,
                DepartmentId  = cfg.DepartmentId,
            });
        }

        // ── Category summary: one GL line per product category ────────────────
        if (cfg.CategoryAccountMap.Count > 0)
        {
            foreach (var cat in report.CategorySummary ?? [])
            {
                // Try specific category first, then fall back to the empty-string default row.
                if (!cfg.CategoryAccountMap.TryGetValue(cat.Category ?? string.Empty, out var catCfg))
                    cfg.CategoryAccountMap.TryGetValue(string.Empty, out catCfg);

                if (catCfg is null) continue;

                var rawAmount = catCfg.AmountSelector switch
                {
                    AmountSelector.Gross    => cat.CategoryGrossTotal,
                    AmountSelector.Discount => cat.CategoryDiscountTotal,
                    AmountSelector.Cost     => cat.CategoryCost,
                    _                       => cat.CategoryNetTotal,
                };

                if (rawAmount == 0) continue;

                // Revenue categories are credits; flip sign when IsCredit is true.
                var amount = catCfg.IsCredit ? -(decimal)rawAmount : (decimal)rawAmount;

                lines.Add(new JournalEntryLine
                {
                    AccountNumber = catCfg.Account,
                    Amount        = amount,
                    Memo          = $"{cat.Category ?? "Category"} sales",
                    LocationId    = catCfg.LocationId    ?? cfg.LocationId,
                    DepartmentId  = catCfg.DepartmentId  ?? cfg.DepartmentId,
                    ClassId       = catCfg.ClassId,
                });
            }
        }

        // ── Customer-type summary: one GL line per customer type ──────────────
        if (cfg.CustomerTypeAccountMap.Count > 0)
        {
            foreach (var ct in report.CustomerTypeSummary ?? [])
            {
                if (!cfg.CustomerTypeAccountMap.TryGetValue(ct.CustomerType ?? string.Empty, out var ctCfg))
                    cfg.CustomerTypeAccountMap.TryGetValue(string.Empty, out ctCfg);

                if (ctCfg is null) continue;

                var rawAmount = ctCfg.AmountSelector switch
                {
                    AmountSelector.Gross    => ct.GrossTotal,
                    AmountSelector.Discount => ct.DiscountTotal,
                    _                       => ct.NetTotal,
                };

                if (rawAmount == 0) continue;

                var amount = ctCfg.IsCredit ? -(decimal)rawAmount : (decimal)rawAmount;

                lines.Add(new JournalEntryLine
                {
                    AccountNumber = ctCfg.Account,
                    Amount        = amount,
                    Memo          = $"{ct.CustomerType ?? "Customer type"} sales",
                    LocationId    = ctCfg.LocationId   ?? cfg.LocationId,
                    DepartmentId  = ctCfg.DepartmentId ?? cfg.DepartmentId,
                    ClassId       = ctCfg.ClassId,
                });
            }
        }

        // ── Rounding / final adjustment ───────────────────────────────────────
        // Ensure the journal entry balances to zero. Add a residual line when it does not.
        if (!string.IsNullOrEmpty(cfg.RoundingAccount))
        {
            var netBalance = lines.Sum(l => l.Amount);
            if (netBalance != 0m)
            {
                var absBalance = Math.Abs(netBalance);
                if (absBalance > cfg.MaximumOverShort)
                    _logger.LogWarning(
                        "Closing report over/short of {Amount:C} exceeds threshold of {Threshold:C}. " +
                        "Adding rounding adjustment to {Account}.",
                        absBalance, cfg.MaximumOverShort, cfg.RoundingAccount);
                else
                    _logger.LogDebug(
                        "Adding rounding adjustment of {Amount:C} to account {Account}.",
                        -netBalance, cfg.RoundingAccount);

                lines.Add(new JournalEntryLine
                {
                    AccountNumber = cfg.RoundingAccount,
                    Amount        = -netBalance,   // flip to zero out the batch
                    Memo          = "Rounding adjustment",
                    LocationId    = cfg.LocationId,
                    DepartmentId  = cfg.DepartmentId,
                });
            }
        }

        return new JournalEntryPayload
        {
            ReferenceNumber = $"DUTCHIE-CLOSE-{date:yyyyMMdd}",
            Date            = date,
            Description     = $"Dutchie closing report {from:yyyy-MM-dd} – {to:yyyy-MM-dd}",
            LocationId      = cfg.LocationId,
            JournalSymbol   = cfg.JournalSymbol,
            PostAsDraft     = !cfg.IsLive,
            Lines           = lines,
        };
    }
}
