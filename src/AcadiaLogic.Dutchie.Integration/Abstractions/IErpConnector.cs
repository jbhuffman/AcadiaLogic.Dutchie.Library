using AcadiaLogic.Dutchie.Integration.Models;

namespace AcadiaLogic.Dutchie.Integration.Abstractions;

/// <summary>
/// ERP-neutral contract for posting financial data.
/// Implement this interface for any target ERP (Sage Intacct, QuickBooks, NetSuite, etc.).
/// </summary>
public interface IErpConnector
{
    /// <summary>
    /// Posts a GL journal entry summarising a Dutchie closing-report period.
    /// </summary>
    /// <returns>The ERP-assigned journal entry key/reference.</returns>
    Task<string> PostJournalEntryAsync(JournalEntryPayload entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a single customer sales transaction as an ERP sales/AR document.
    /// </summary>
    /// <returns>The ERP-assigned document key/reference.</returns>
    Task<string> PostSalesTransactionAsync(SalesTransactionPayload transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ERP connector name (e.g. "SageIntacct", "QuickBooks").
    /// Used for logging and diagnostics.
    /// </summary>
    string ConnectorName { get; }
}
