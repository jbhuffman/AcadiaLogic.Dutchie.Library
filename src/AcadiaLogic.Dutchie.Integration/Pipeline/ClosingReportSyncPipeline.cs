using AcadiaLogic.Dutchie.Clients;
using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using Microsoft.Extensions.Logging;

namespace AcadiaLogic.Dutchie.Integration.Pipeline;

/// <summary>
/// Orchestrates: pull closing report from Dutchie → map to JournalEntryPayload → post to ERP.
/// </summary>
public sealed class ClosingReportSyncPipeline
{
    public const string JobName = "ClosingReport";

    private readonly IReportingClient _reporting;
    private readonly IErpConnector _erp;
    private readonly IErpConfigProvider _config;
    private readonly ISyncStateStore _state;
    private readonly ILogger<ClosingReportSyncPipeline> _logger;

    public ClosingReportSyncPipeline(
        IReportingClient reporting,
        IErpConnector erp,
        IErpConfigProvider config,
        ISyncStateStore state,
        ILogger<ClosingReportSyncPipeline> logger)
    {
        _reporting = reporting;
        _erp = erp;
        _config = config;
        _state = state;
        _logger = logger;
    }

    /// <summary>
    /// Syncs a specific date range. Called by the worker on its schedule.
    /// </summary>
    public async Task RunAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Closing report sync: {From} → {To}", from, to);

        var mappingConfig = await _config.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var report = await _reporting.GetClosingReportAsync(from, to, cancellationToken).ConfigureAwait(false);
        var entry = BuildJournalEntry(report, from, to, mappingConfig);

        var key = await _erp.PostJournalEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Journal entry posted. ERP key: {Key}", key);

        await _state.SetLastSyncTimeAsync(JobName, to, cancellationToken).ConfigureAwait(false);
    }

    private static JournalEntryPayload BuildJournalEntry(
        global::AcadiaLogic.Dutchie.Models.Reporting.ClosingReport report,
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
                Amount = (decimal)payment.TotalPaid,   // debit
                Memo = $"{payment.PaymentType} receipts",
                LocationId = cfg.LocationId,
                DepartmentId = cfg.DepartmentId
            });
        }

        // ── Credit: cannabis sales ────────────────────────────────────────────
        if (report.CannabisSales is > 0 && !string.IsNullOrEmpty(cfg.CannabisSalesAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.CannabisSalesAccount,
                Amount = -(decimal)report.CannabisSales.Value,  // credit
                Memo = "Cannabis net sales",
                LocationId = cfg.LocationId,
                DepartmentId = cfg.DepartmentId
            });
        }

        // ── Credit: non-cannabis sales ────────────────────────────────────────
        if (report.NonCannabisSales is > 0 && !string.IsNullOrEmpty(cfg.NonCannabisSalesAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.NonCannabisSalesAccount,
                Amount = -(decimal)report.NonCannabisSales.Value,   // credit
                Memo = "Non-cannabis net sales",
                LocationId = cfg.LocationId,
                DepartmentId = cfg.DepartmentId
            });
        }

        // ── Debit: discounts given ────────────────────────────────────────────
        if (report.Discount is > 0 && !string.IsNullOrEmpty(cfg.DiscountAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.DiscountAccount,
                Amount = (decimal)report.Discount.Value,    // debit (contra-revenue)
                Memo = "Sales discounts",
                LocationId = cfg.LocationId,
                DepartmentId = cfg.DepartmentId
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
                Amount = -(decimal)tax.TotalTax,    // credit (liability)
                Memo = $"Tax: {tax.TaxRate}",
                LocationId = cfg.LocationId,
                DepartmentId = cfg.DepartmentId
            });
        }

        // ── Credit: tips ──────────────────────────────────────────────────────
        if (report.TotalTips is > 0 && !string.IsNullOrEmpty(cfg.TipsAccount))
        {
            lines.Add(new JournalEntryLine
            {
                AccountNumber = cfg.TipsAccount,
                Amount = -(decimal)report.TotalTips.Value,  // credit
                Memo = "Tips",
                LocationId = cfg.LocationId,
                DepartmentId = cfg.DepartmentId
            });
        }

        return new JournalEntryPayload
        {
            ReferenceNumber = $"DUTCHIE-CLOSE-{date:yyyyMMdd}",
            Date = date,
            Description = $"Dutchie closing report {from:yyyy-MM-dd} – {to:yyyy-MM-dd}",
            LocationId = cfg.LocationId,
            Lines = lines
        };
    }
}
