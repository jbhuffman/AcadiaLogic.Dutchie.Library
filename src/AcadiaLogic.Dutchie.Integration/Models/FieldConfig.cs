namespace AcadiaLogic.Dutchie.Integration.Models;

/// <summary>
/// Controls which dollar figure from a Dutchie summary row is used when building a GL entry.
/// Applies to <c>categorysummary</c> and <c>customertypesummary</c> rows.
/// </summary>
public enum AmountSelector
{
    /// <summary>Net sales after discounts (default).</summary>
    Net,
    /// <summary>Gross sales before discounts.</summary>
    Gross,
    /// <summary>Discount total (contra-revenue).</summary>
    Discount,
    /// <summary>Cost of goods sold.</summary>
    Cost,
}

/// <summary>
/// GL entry specification for a single category or customer-type summary row.
/// Used by <see cref="ErpMappingConfig.CategoryAccountMap"/> and
/// <see cref="ErpMappingConfig.CustomerTypeAccountMap"/>.
/// The dictionary key is the Dutchie category/customer-type name; an empty-string key
/// acts as the default fallback when no specific row is configured for a given name.
/// </summary>
public sealed class SummaryLineConfig
{
    /// <summary>ERP GL account number for this entry.</summary>
    public required string Account { get; init; }

    /// <summary>Which dollar figure to pull from the summary row.</summary>
    public AmountSelector AmountSelector { get; init; } = AmountSelector.Net;

    /// <summary>
    /// When <see langword="true"/> the entry is posted as a credit (negative amount).
    /// When <see langword="false"/> it is posted as a debit (positive amount).
    /// Revenue lines are typically credits; discount/COGS lines are typically debits.
    /// </summary>
    public bool IsCredit { get; init; } = true;

    /// <summary>Per-entry department dimension override. Falls back to location-level default when null.</summary>
    public string? DepartmentId { get; init; }

    /// <summary>Intacct Class dimension for cost-centre / project tracking.</summary>
    public string? ClassId { get; init; }

    /// <summary>Per-entry location dimension override. Falls back to location-level default when null.</summary>
    public string? LocationId { get; init; }
}
