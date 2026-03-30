using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dutchie.Clients;

/// <summary>
/// Creates <see cref="IReportingClient"/> instances authenticated with per-location credentials.
/// Uses the <c>Dutchie.PerLocation</c> named <see cref="HttpClient"/> (no auth handler attached)
/// and stamps the Authorization header directly for the requested location.
/// Falls back to <see cref="DutchieClientOptions"/> when credentials are not supplied.
/// </summary>
internal sealed class DutchieClientFactory : IDutchieClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DutchieClientOptions _defaultOptions;

    public DutchieClientFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<DutchieClientOptions> defaultOptions)
    {
        _httpClientFactory  = httpClientFactory;
        _loggerFactory      = loggerFactory;
        _defaultOptions     = defaultOptions.Value;
    }

    public IReportingClient CreateReportingClient(string? locationKey, string? integratorKey)
    {
        var key    = string.IsNullOrWhiteSpace(locationKey)    ? _defaultOptions.LocationKey    : locationKey;
        var secret = string.IsNullOrWhiteSpace(integratorKey)  ? _defaultOptions.IntegratorKey  : integratorKey;

        // IHttpClientFactory creates a fresh HttpClient instance per call.
        // The underlying HttpClientHandler is pooled; setting DefaultRequestHeaders on the
        // client instance is safe and does not bleed into other calls.
        var httpClient = _httpClientFactory.CreateClient(DutchieServiceCollectionExtensions.PerLocationClientName);

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret ?? string.Empty}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        return new ReportingClient(httpClient, _loggerFactory.CreateLogger<ReportingClient>());
    }
}
