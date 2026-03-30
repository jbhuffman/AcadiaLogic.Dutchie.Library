using Dutchie.Integration.Abstractions;
using Dutchie.Integration.Pipeline;
using Dutchie.Integration.State;
using Microsoft.Extensions.DependencyInjection;

namespace Dutchie.Integration;

public static class IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the sync pipelines and the default JSON-file state store.
    /// Call after <c>AddDutchieClient(...)</c> and your ERP connector registration.
    /// </summary>
    public static IServiceCollection AddDutchieIntegration(
        this IServiceCollection services,
        Action<JsonFileSyncStateStoreOptions>? configureState = null)
    {
        services.Configure<JsonFileSyncStateStoreOptions>(opts =>
        {
            configureState?.Invoke(opts);
        });

        services.AddSingleton<ISyncStateStore, JsonFileSyncStateStore>();
        services.AddTransient<ClosingReportSyncPipeline>();
        services.AddTransient<TransactionSyncPipeline>();

        return services;
    }
}
