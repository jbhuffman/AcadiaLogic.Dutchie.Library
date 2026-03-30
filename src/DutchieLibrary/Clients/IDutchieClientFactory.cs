namespace Dutchie.Clients;

/// <summary>
/// Creates per-location Dutchie API clients at runtime.
/// Use this when credentials differ per Intacct location (i.e. sourced from
/// <c>dutchie_location_config</c> rather than a single env-var pair).
/// </summary>
public interface IDutchieClientFactory
{
    /// <summary>
    /// Creates an <see cref="IReportingClient"/> authenticated with the supplied credentials.
    /// When either credential is null or empty, falls back to the globally configured
    /// <see cref="DutchieClientOptions"/> (env-var / appsettings values).
    /// </summary>
    IReportingClient CreateReportingClient(string? locationKey, string? integratorKey);
}
