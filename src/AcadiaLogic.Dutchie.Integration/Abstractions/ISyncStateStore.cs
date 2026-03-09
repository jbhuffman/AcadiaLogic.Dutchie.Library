namespace AcadiaLogic.Dutchie.Integration.Abstractions;

/// <summary>
/// Persists and retrieves the watermark timestamps used for incremental sync.
/// Implement this interface to back state with a file, database, or cloud store.
/// A simple JSON-file implementation is provided as the default.
/// </summary>
public interface ISyncStateStore
{
    /// <summary>Returns the last successful sync time for a named job, or null if never run.</summary>
    Task<DateTimeOffset?> GetLastSyncTimeAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>Persists the completion time for a named job after a successful sync.</summary>
    Task SetLastSyncTimeAsync(string jobName, DateTimeOffset syncTime, CancellationToken cancellationToken = default);
}
