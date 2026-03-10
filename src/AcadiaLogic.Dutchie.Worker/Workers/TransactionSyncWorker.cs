using AcadiaLogic.Dutchie.Clients;
using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie.Worker.Workers;

/// <summary>
/// Background worker that incrementally syncs Dutchie transactions to Intacct AR invoices
/// on a configurable schedule. Runs the sync for every configured location in sequence.
/// Each location maintains its own watermark; a failure for one location is logged
/// and written to <c>dutchie_process_log</c>, then the worker moves on to the next location.
/// </summary>
public sealed class TransactionSyncWorker : BackgroundService
{
    private readonly TransactionSyncPipeline _pipeline;
    private readonly IErpConfigProvider _configProvider;
    private readonly IDutchieClientFactory _clientFactory;
    private readonly WorkerOptions _options;
    private readonly ILogger<TransactionSyncWorker> _logger;

    public TransactionSyncWorker(
        TransactionSyncPipeline pipeline,
        IErpConfigProvider configProvider,
        IDutchieClientFactory clientFactory,
        IOptions<WorkerOptions> options,
        ILogger<TransactionSyncWorker> logger)
    {
        _pipeline       = pipeline;
        _configProvider = configProvider;
        _clientFactory  = clientFactory;
        _options        = options.Value;
        _logger         = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TransactionSyncWorker started. Interval: {Interval}", _options.TransactionSyncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAllLocationsAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(_options.TransactionSyncInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("TransactionSyncWorker stopped.");
    }

    private async Task RunAllLocationsAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Integration.Models.ErpMappingConfig> configs;

        try
        {
            configs = await _configProvider.GetAllConfigsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TransactionSyncWorker failed to load location configs from Intacct. Skipping this run.");
            return;
        }

        if (configs.Count == 0)
        {
            _logger.LogWarning("TransactionSyncWorker: no location configs found. Nothing to sync.");
            return;
        }

        _logger.LogInformation("TransactionSyncWorker: syncing {Count} location(s).", configs.Count);

        foreach (var cfg in configs)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var client = _clientFactory.CreateReportingClient(cfg.DutchieLocationKey, cfg.DutchieIntegratorKey);
                await _pipeline.RunAsync(client, cfg, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Pipeline already wrote a failed process_log entry and logged the error.
                // Swallow here so remaining locations are not skipped.
                _logger.LogError(ex,
                    "TransactionSyncWorker: sync failed for location {LocationId}. Moving to next location.",
                    cfg.LocationId ?? "(no location ID)");
            }
        }
    }
}
