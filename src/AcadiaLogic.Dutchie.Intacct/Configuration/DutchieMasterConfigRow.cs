namespace AcadiaLogic.Dutchie.Intacct.Configuration;

/// <summary>
/// Represents a record from the <c>dutchie_master_config</c> Intacct Platform Application
/// custom object. Company-level settings shared across all locations.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>Property</term><term>Intacct Field</term></listheader>
///   <item><term><see cref="RecordNo"/></term><term>RECORDNO</term></item>
///   <item><term><see cref="GlJournalSymbol"/></term><term>RGLJOURNAL</term></item>
///   <item><term><see cref="MaximumOverShort"/></term><term>maximum_overshort (DBL0)</term></item>
///   <item><term><see cref="IsLive"/></term><term>is_live (BOOL0)</term></item>
/// </list>
/// </remarks>
public sealed class DutchieMasterConfigRow
{
    /// <summary>Intacct system-assigned record number.</summary>
    public string? RecordNo { get; init; }

    /// <summary>GL journal symbol to use for closing-report journal entries (e.g. "GJ", "APJ").</summary>
    public string? GlJournalSymbol { get; init; }

    /// <summary>
    /// Maximum acceptable cash over/short discrepancy (absolute $). Discrepancies within this
    /// threshold are absorbed; beyond it a warning is raised. Defaults to 1.00.
    /// </summary>
    public decimal MaximumOverShort { get; init; } = 1m;

    /// <summary>
    /// When <see langword="true"/>, journal entries are posted immediately.
    /// When <see langword="false"/>, they are held as Draft for manual review.
    /// </summary>
    public bool IsLive { get; init; }

    /// <summary>
    /// Parses an <see cref="System.Xml.Linq.XElement"/> row returned by the Intacct SDK
    /// <c>QueryFunction</c> into a <see cref="DutchieMasterConfigRow"/>.
    /// </summary>
    public static DutchieMasterConfigRow FromXElement(System.Xml.Linq.XElement element)
    {
        static string? Val(System.Xml.Linq.XElement el, string name)
        {
            var v = el.Element(name)?.Value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        return new DutchieMasterConfigRow
        {
            RecordNo        = Val(element, "RECORDNO"),
            GlJournalSymbol = Val(element, "RGLJOURNAL"),
            MaximumOverShort = decimal.TryParse(Val(element, "maximum_overshort"), out var mos) ? mos : 1m,
            IsLive          = Val(element, "is_live") is "true" or "1",
        };
    }
}
