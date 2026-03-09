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

    /// <summary>Free-form dimension/tag map for ERP-specific dimensions (e.g. Class, Project).</summary>
    public Dictionary<string, string>? Dimensions { get; init; }
}
