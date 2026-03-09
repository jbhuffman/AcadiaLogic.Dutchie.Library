namespace AcadiaLogic.Dutchie.Models.Reporting;

/// <summary>
/// Real-time cash activity summary per register.
/// Returned by GET /reporting/cash-summary. fromLastModifiedDateUTC must be within the last 7 days.
/// </summary>
public sealed class RegisterCashSummary
{
    public string? TerminalName { get; init; }
    public double StartingBalance { get; init; }
    public double EndingBalance { get; init; }
    public double Sales { get; init; }
    public double Returns { get; init; }
    public double Deposits { get; init; }
    public double Adjustments { get; init; }
    public double OverShort { get; init; }
}
