using AcadiaLogic.Dutchie.Integration.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AcadiaLogic.Dutchie.Integration.State;

/// <summary>
/// Simple file-based sync state store. Persists watermarks to a JSON file on disk.
/// Suitable for single-instance deployments. Replace with a database-backed store for HA scenarios.
/// </summary>
public sealed class JsonFileSyncStateStore : ISyncStateStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonFileSyncStateStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented };

    public JsonFileSyncStateStore(IOptions<JsonFileSyncStateStoreOptions> options, ILogger<JsonFileSyncStateStore> logger)
    {
        _filePath = options.Value.FilePath;
        _logger = logger;
    }

    public async Task<DateTimeOffset?> GetLastSyncTimeAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return state.TryGetValue(jobName, out var ts) ? ts : null;
    }

    public async Task SetLastSyncTimeAsync(string jobName, DateTimeOffset syncTime, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            state[jobName] = syncTime;
            var json = JsonConvert.SerializeObject(state, JsonSettings);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Sync state updated: {Job} = {Time}", jobName, syncTime);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, DateTimeOffset>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Dictionary<string, DateTimeOffset>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read sync state file at {Path}; starting fresh", _filePath);
            return [];
        }
    }
}

public sealed class JsonFileSyncStateStoreOptions
{
    public string FilePath { get; set; } = "sync-state.json";
}
