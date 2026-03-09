using AcadiaLogic.Dutchie.Integration.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie.Worker.Workers;

/// <summary>
/// Background worker that incrementally syncs Dutchie transactions to Intacct AR invoices on a configurable schedule.
/// </summary>
public sealed class TransactionSyncWorker : BackgroundService
{
    private readonly TransactionSyncPipeline _pipeline;
    private readonly WorkerOptions _options;
    private readonly ILogger<TransactionSyncWorker> _logger;

    public TransactionSyncWorker(
        TransactionSyncPipeline pipeline,
        IOptions<WorkerOptions> options,
        ILogger<TransactionSyncWorker> logger)
    {
        _pipeline = pipeline;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TransactionSyncWorker started. Interval: {Interval}", _options.TransactionSyncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _pipeline.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionSyncWorker encountered an error. Will retry after interval.");
            }

            await Task.Delay(_options.TransactionSyncInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("TransactionSyncWorker stopped.");
    }
}
