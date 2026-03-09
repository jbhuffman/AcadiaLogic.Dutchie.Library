namespace AcadiaLogic.Dutchie.Integration.Models;

/// <summary>
/// ERP-neutral representation of a GL journal entry built from a Dutchie closing report.
/// The ERP connector translates this into the target system's native API objects.
/// </summary>
public sealed class JournalEntryPayload
{
    /// <summary>Human-readable reference (e.g. "Dutchie Closing 2025-02-18").</summary>
    public required string ReferenceNumber { get; init; }

    public required DateOnly Date { get; init; }
    public string? Description { get; init; }

    /// <summary>Source location identifier (for multi-location setups).</summary>
    public string? LocationId { get; init; }

    /// <summary>
    /// ERP journal symbol (e.g. "GJ", "APJ"). Sourced from <c>ErpMappingConfig.JournalSymbol</c>
    /// and carried here so the connector does not need a separate config lookup.
    /// </summary>
    public string JournalSymbol { get; init; } = "GJ";

    /// <summary>
    /// When <see langword="true"/> the ERP connector should hold the entry as a Draft
    /// rather than posting it immediately. Driven by <c>ErpMappingConfig.IsLive</c>.
    /// </summary>
    public bool PostAsDraft { get; init; }

    public required IReadOnlyList<JournalEntryLine> Lines { get; init; }
}

public sealed class JournalEntryLine
{
    /// <summary>GL account number in the target ERP.</summary>
    public required string AccountNumber { get; init; }

    /// <summary>Positive = debit, negative = credit.</summary>
    public required decimal Amount { get; init; }

    public string? Memo { get; init; }
    public string? LocationId { get; init; }
    public string? DepartmentId { get; init; }

    /// <summary>Intacct Class dimension for cost-centre / project tracking.</summary>
    public string? ClassId { get; init; }

    /// <summary>Free-form dimension/tag map for ERP-specific dimensions.</summary>
    public Dictionary<string, string>? Dimensions { get; init; }
}
