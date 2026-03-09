namespace AcadiaLogic.Dutchie.Models.Reporting;

/// <summary>
/// A register-level transaction record (sale, return, adjustment, close-out, deposit, withdrawal).
/// Returned by GET /reporting/register-transactions.
/// </summary>
public sealed class RegisterTransaction
{
    public int RegisterTransactionId { get; init; }
    /// <summary>Sale, Return, Adjustment, Close Out, Deposit, Withdrawal, or Payment.</summary>
    public string? TransactionType { get; init; }
    public double TransactionAmount { get; init; }
    public string? TransactionBy { get; init; }
    public DateTimeOffset? TransactionDateUtc { get; init; }
    /// <summary>Associated customer sale/return ID; null for non-sale activity.</summary>
    public int? TransactionId { get; init; }
    public string? TerminalName { get; init; }
    public int TerminalId { get; init; }
    public int TransactionByEmployeeId { get; init; }
    public string? AdjustmentReason { get; init; }
    public string? Comment { get; init; }
}
