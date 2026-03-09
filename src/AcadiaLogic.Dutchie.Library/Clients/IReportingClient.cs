using AcadiaLogic.Dutchie.Models.Reporting;

namespace AcadiaLogic.Dutchie.Clients;

public interface IReportingClient
{
    /// <summary>
    /// Returns a comprehensive financial closing report.
    /// Date range must be between 12 hours and 31 days.
    /// Rate limit: 120/min.
    /// </summary>
    Task<ClosingReport> GetClosingReportAsync(
        DateTimeOffset fromDateUtc,
        DateTimeOffset toDateUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns register-level transaction history (sales, returns, adjustments, close-outs, deposits).
    /// Use date filters — can return very large datasets without them.
    /// Rate limit: 120/min.
    /// </summary>
    Task<IReadOnlyList<RegisterTransaction>> GetRegisterTransactionsAsync(
        DateTimeOffset? fromLastModifiedDateUtc = null,
        DateTimeOffset? toLastModifiedDateUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full POS and wholesale transactions with optional line item, tax, and fee detail.
    /// TransactionId, date range, and last-modified range are mutually exclusive.
    /// Rate limit: 600/min.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        TransactionQueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns real-time cash activity summaries per register (starting/ending balance, sales, over/short).
    /// fromLastModifiedDateUtc must be within the last 7 days.
    /// Rate limit: 480/min.
    /// </summary>
    Task<IReadOnlyList<RegisterCashSummary>> GetCashSummaryAsync(
        DateTimeOffset? fromLastModifiedDateUtc = null,
        DateTimeOffset? toLastModifiedDateUtc = null,
        CancellationToken cancellationToken = default);
}
