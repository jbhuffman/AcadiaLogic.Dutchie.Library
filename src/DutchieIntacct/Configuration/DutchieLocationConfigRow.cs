namespace Dutchie.Intacct.Configuration;

/// <summary>
/// Represents a record from the <c>dutchie_location_config</c> Intacct Platform Application
/// custom object. One record per Intacct Location that participates in Dutchie sync.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>Property</term><term>Intacct Field</term><term>Notes</term></listheader>
///   <item><term><see cref="RecordNo"/></term><term>RECORDNO</term><term>System-assigned; used as foreign key by <c>dutchie_field_config</c> rows</term></item>
///   <item><term><see cref="MasterConfigRecordNo"/></term><term>Rdutchiemasterconfig</term><term>Links to the company-level <c>dutchie_master_config</c></term></item>
///   <item><term><see cref="LocationId"/></term><term>RLOC</term><term>Intacct Location ID</term></item>
///   <item><term><see cref="EntityId"/></term><term>entity_id (STR0)</term><term>Optional SDK EntityId override for multi-entity companies</term></item>
///   <item><term><see cref="DutchieLocationKey"/></term><term>dutchie_location_key (STR1)</term><term>Dutchie Basic Auth username</term></item>
///   <item><term><see cref="DutchieIntegratorKey"/></term><term>dutchie_integrator_key (STR2)</term><term>Dutchie Basic Auth password</term></item>
///   <item><term><see cref="DefaultCustomerId"/></term><term>RCUSTOMER (INTG2)</term><term>Walk-in / default customer for unmatched transactions</term></item>
///   <item><term><see cref="DefaultDepartmentId"/></term><term>RDEPARTMENT (INTG3)</term><term>Default department dimension for journal/invoice lines</term></item>
///   <item><term><see cref="DefaultItemId"/></term><term>RITEM (INTG4)</term><term>Default item for AR invoice lines</term></item>
/// </list>
/// </remarks>
public sealed class DutchieLocationConfigRow
{
    /// <summary>Intacct system-assigned record number. Used as the foreign key in <c>dutchie_field_config</c>.</summary>
    public string? RecordNo { get; init; }

    /// <summary>Record number of the <c>dutchie_master_config</c> this location uses.</summary>
    public string? MasterConfigRecordNo { get; init; }

    /// <summary>Intacct Location ID (from the RLOC relationship).</summary>
    public string? LocationId { get; init; }

    /// <summary>
    /// Optional Intacct Entity ID override for multi-entity companies where the SDK
    /// <c>ClientConfig.EntityId</c> differs from the Location's own entity.
    /// Leave <see langword="null"/> or empty to use the location's natural entity context.
    /// </summary>
    public string? EntityId { get; init; }

    /// <summary>Dutchie LocationKey (Basic Auth username) for this Intacct location.</summary>
    public string? DutchieLocationKey { get; init; }

    /// <summary>Dutchie IntegratorKey (Basic Auth password) for this Intacct location.</summary>
    public string? DutchieIntegratorKey { get; init; }

    /// <summary>Default Intacct customer ID for Dutchie transactions with no matching ERP customer.</summary>
    public string? DefaultCustomerId { get; init; }

    /// <summary>Default Intacct department ID to stamp on journal and invoice lines for this location.</summary>
    public string? DefaultDepartmentId { get; init; }

    /// <summary>Default Intacct item ID for AR invoice lines at this location.</summary>
    public string? DefaultItemId { get; init; }

    /// <summary>
    /// Parses an <see cref="System.Xml.Linq.XElement"/> row returned by the Intacct SDK
    /// <c>QueryFunction</c> into a <see cref="DutchieLocationConfigRow"/>.
    /// </summary>
    public static DutchieLocationConfigRow FromXElement(System.Xml.Linq.XElement element)
    {
        static string? Val(System.Xml.Linq.XElement el, string name)
        {
            var v = el.Element(name)?.Value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        return new DutchieLocationConfigRow
        {
            RecordNo             = Val(element, "RECORDNO"),
            MasterConfigRecordNo = Val(element, "Rdutchiemasterconfig"),
            LocationId           = Val(element, "RLOC"),
            EntityId             = Val(element, "entity_id"),
            DutchieLocationKey   = Val(element, "dutchie_location_key"),
            DutchieIntegratorKey = Val(element, "dutchie_integrator_key"),
            DefaultCustomerId    = Val(element, "RCUSTOMER"),
            DefaultDepartmentId  = Val(element, "RDEPARTMENT"),
            DefaultItemId        = Val(element, "RITEM"),
        };
    }
}
