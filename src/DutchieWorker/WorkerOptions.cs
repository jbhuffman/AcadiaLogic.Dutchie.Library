namespace Dutchie.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>How often the closing report sync runs. Default: every 24 hours.</summary>
    public TimeSpan ClosingReportInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>The lookback window for each closing report pull. Default: 24 hours. Must be 12h–31d per API rules.</summary>
    public TimeSpan ClosingReportLookback { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How often the transaction sync runs. Default: every 15 minutes.</summary>
    public TimeSpan TransactionSyncInterval { get; set; } = TimeSpan.FromMinutes(15);
}
