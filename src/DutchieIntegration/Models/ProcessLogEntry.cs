namespace Dutchie.Integration.Models;

/// <summary>
/// ERP-neutral process log entry written to the ERP after each sync run.
/// For Sage Intacct, this maps to the <c>dutchie_process_log</c> Platform App object.
/// </summary>
public sealed class ProcessLogEntry
{
    /// <summary>Job identifier — matches the pipeline's <c>JobName</c> constant (e.g. "ClosingReport", "Transactions").</summary>
    public required string JobName { get; init; }

    /// <summary>Run outcome. Use <see cref="Statuses"/> constants.</summary>
    public required string Status { get; init; }

    /// <summary>Number of ERP records successfully created or updated during this run.</summary>
    public int RecordsProcessed { get; init; }

    /// <summary>Full exception text for failed runs. Truncated at 4000 chars before writing to Intacct.</summary>
    public string? RawErrors { get; init; }

    /// <summary>Short human-readable error summary (e.g. <c>Exception.Message</c>).</summary>
    public string? SummarizedErrors { get; init; }

    /// <summary>
    /// RECORDNO of the <c>dutchie_location_config</c> record in Intacct.
    /// Links this log entry to the location it belongs to.
    /// Populated from <see cref="ErpMappingConfig.LocationConfigRecordNo"/>.
    /// </summary>
    public string? LocationConfigRecordNo { get; init; }

    /// <summary>Valid values for <see cref="Status"/>.</summary>
    public static class Statuses
    {
        public const string Complete   = "complete";
        public const string Failed     = "failed";
        public const string InProgress = "in_progress";
        public const string InQueue    = "in_queue";
    }
}
