using Dutchie.Clients;
using Dutchie.Integration.Abstractions;
using Dutchie.Integration.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dutchie.Worker.Workers;

/// <summary>
/// Background worker that posts a Dutchie closing report as a GL journal entry to Intacct
/// on a configurable schedule. Runs the sync for every configured location in sequence.
/// A failure for one location is logged and written to <c>dutchie_process_log</c>,
/// then the worker moves on to the next location.
/// </summary>
public sealed class ClosingReportWorker : BackgroundService
{
    private readonly ClosingReportSyncPipeline _pipeline;
    private readonly IErpConfigProvider _configProvider;
    private readonly IDutchieClientFactory _clientFactory;
    private readonly WorkerOptions _options;
    private readonly ILogger<ClosingReportWorker> _logger;

    public ClosingReportWorker(
        ClosingReportSyncPipeline pipeline,
        IErpConfigProvider configProvider,
        IDutchieClientFactory clientFactory,
        IOptions<WorkerOptions> options,
        ILogger<ClosingReportWorker> logger)
    {
        _pipeline      = pipeline;
        _configProvider = configProvider;
        _clientFactory = clientFactory;
        _options       = options.Value;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClosingReportWorker started. Interval: {Interval}", _options.ClosingReportInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAllLocationsAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(_options.ClosingReportInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ClosingReportWorker stopped.");
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
            _logger.LogError(ex, "ClosingReportWorker failed to load location configs from Intacct. Skipping this run.");
            return;
        }

        if (configs.Count == 0)
        {
            _logger.LogWarning("ClosingReportWorker: no location configs found. Nothing to sync.");
            return;
        }

        _logger.LogInformation("ClosingReportWorker: syncing {Count} location(s).", configs.Count);

        var to   = DateTimeOffset.UtcNow;
        var from = to - _options.ClosingReportLookback;

        foreach (var cfg in configs)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var client = _clientFactory.CreateReportingClient(cfg.DutchieLocationKey, cfg.DutchieIntegratorKey);
                await _pipeline.RunAsync(from, to, client, cfg, stoppingToken).ConfigureAwait(false);
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
                    "ClosingReportWorker: sync failed for location {LocationId}. Moving to next location.",
                    cfg.LocationId ?? "(no location ID)");
            }
        }
    }
}
