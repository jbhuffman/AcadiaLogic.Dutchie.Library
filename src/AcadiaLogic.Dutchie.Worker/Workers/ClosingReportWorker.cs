using AcadiaLogic.Dutchie.Integration.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie.Worker.Workers;

/// <summary>
/// Background worker that posts a Dutchie closing report as a GL journal entry to Intacct on a configurable schedule.
/// </summary>
public sealed class ClosingReportWorker : BackgroundService
{
    private readonly ClosingReportSyncPipeline _pipeline;
    private readonly WorkerOptions _options;
    private readonly ILogger<ClosingReportWorker> _logger;

    public ClosingReportWorker(
        ClosingReportSyncPipeline pipeline,
        IOptions<WorkerOptions> options,
        ILogger<ClosingReportWorker> logger)
    {
        _pipeline = pipeline;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClosingReportWorker started. Interval: {Interval}", _options.ClosingReportInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var to = DateTimeOffset.UtcNow;
                var from = to - _options.ClosingReportLookback;
                await _pipeline.RunAsync(from, to, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClosingReportWorker encountered an error. Will retry after interval.");
            }

            await Task.Delay(_options.ClosingReportInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ClosingReportWorker stopped.");
    }
}
