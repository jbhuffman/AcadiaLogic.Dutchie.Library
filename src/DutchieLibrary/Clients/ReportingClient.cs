using Dutchie.Models.Reporting;
using Microsoft.Extensions.Logging;

namespace Dutchie.Clients;

internal sealed class ReportingClient : DutchieClientBase, IReportingClient
{
    public ReportingClient(HttpClient http, ILogger<ReportingClient> logger) : base(http, logger) { }

    public Task<ClosingReport> GetClosingReportAsync(
        DateTimeOffset fromDateUtc,
        DateTimeOffset toDateUtc,
        CancellationToken cancellationToken = default)
    {
        var qs = BuildQueryString(q =>
        {
            Add(q, "fromDateUTC", fromDateUtc);
            Add(q, "toDateUTC", toDateUtc);
        });
        return GetAsync<ClosingReport>($"/reporting/closing-report{qs}", cancellationToken);
    }

    public Task<IReadOnlyList<RegisterTransaction>> GetRegisterTransactionsAsync(
        DateTimeOffset? fromLastModifiedDateUtc = null,
        DateTimeOffset? toLastModifiedDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        var qs = BuildQueryString(q =>
        {
            Add(q, "fromLastModifiedDateUTC", fromLastModifiedDateUtc);
            Add(q, "toLastModifiedDateUTC", toLastModifiedDateUtc);
        });
        return GetAsync<IReadOnlyList<RegisterTransaction>>($"/reporting/register-transactions{qs}", cancellationToken);
    }

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        TransactionQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var qs = BuildQueryString(q =>
        {
            Add(q, "TransactionId", request.TransactionId);
            Add(q, "FromLastModifiedDateUTC", request.FromLastModifiedDateUtc);
            Add(q, "ToLastModifiedDateUTC", request.ToLastModifiedDateUtc);
            Add(q, "FromDateUTC", request.FromDateUtc);
            Add(q, "ToDateUTC", request.ToDateUtc);
            Add(q, "IncludeDetail", request.IncludeDetail);
            Add(q, "IncludeTaxes", request.IncludeTaxes);
            Add(q, "IncludeOrderIds", request.IncludeOrderIds);
            Add(q, "IncludeFeesAndDonations", request.IncludeFeesAndDonations);
        });
        return GetAsync<IReadOnlyList<Transaction>>($"/reporting/transactions{qs}", cancellationToken);
    }

    public Task<IReadOnlyList<RegisterCashSummary>> GetCashSummaryAsync(
        DateTimeOffset? fromLastModifiedDateUtc = null,
        DateTimeOffset? toLastModifiedDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        var qs = BuildQueryString(q =>
        {
            Add(q, "fromLastModifiedDateUTC", fromLastModifiedDateUtc);
            Add(q, "toLastModifiedDateUTC", toLastModifiedDateUtc);
        });
        return GetAsync<IReadOnlyList<RegisterCashSummary>>($"/reporting/cash-summary{qs}", cancellationToken);
    }
}
