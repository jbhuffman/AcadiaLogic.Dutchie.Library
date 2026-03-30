namespace Dutchie.Models.Reporting;

/// <summary>
/// Query parameters for GET /reporting/transactions.
/// Note: TransactionId, date range, and last-modified range are mutually exclusive.
/// </summary>
public sealed class TransactionQueryRequest
{
    /// <summary>Single transaction lookup. Mutually exclusive with date filters.</summary>
    public int? TransactionId { get; init; }

    /// <summary>Incremental sync: start of last-modified window (UTC). Mutually exclusive with TransactionId and FromDateUtc/ToDateUtc.</summary>
    public DateTimeOffset? FromLastModifiedDateUtc { get; init; }

    /// <summary>Incremental sync: end of last-modified window (UTC).</summary>
    public DateTimeOffset? ToLastModifiedDateUtc { get; init; }

    /// <summary>Periodic report: start of transaction date window (UTC). Mutually exclusive with TransactionId and last-modified filters.</summary>
    public DateTimeOffset? FromDateUtc { get; init; }

    /// <summary>Periodic report: end of transaction date window (UTC).</summary>
    public DateTimeOffset? ToDateUtc { get; init; }

    /// <summary>Include detailed line item information.</summary>
    public bool? IncludeDetail { get; init; }

    /// <summary>Include per-item tax breakdown.</summary>
    public bool? IncludeTaxes { get; init; }

    /// <summary>Include pre-order IDs linked to each transaction.</summary>
    public bool? IncludeOrderIds { get; init; }

    /// <summary>Include fees and donations line items.</summary>
    public bool? IncludeFeesAndDonations { get; init; }
}
