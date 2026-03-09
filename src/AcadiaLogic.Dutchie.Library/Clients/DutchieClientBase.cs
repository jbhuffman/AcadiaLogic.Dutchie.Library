using System.Collections.Specialized;
using System.Net;
using System.Web;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace AcadiaLogic.Dutchie.Clients;

internal abstract class DutchieClientBase
{
    private const int MaxRetries        = 5;
    private const int RetryDelaySeconds = 3;

    protected static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver  = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Converters        = { new StringEnumConverter(new CamelCaseNamingStrategy()) }
    };

    protected readonly HttpClient Http;
    private readonly ILogger _logger;

    protected DutchieClientBase(HttpClient http, ILogger logger)
    {
        Http    = http;
        _logger = logger;
    }

    protected async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = null!;

        for (int attempt = 1; attempt <= MaxRetries + 1; attempt++)
        {
            response = await Http.GetAsync(path, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.InternalServerError)
                return await ReadResponseAsync<T>(response, cancellationToken).ConfigureAwait(false);

            if (attempt <= MaxRetries)
            {
                _logger.LogWarning(
                    "Dutchie API returned 500 on attempt {Attempt}/{MaxRetries} for {Path}. " +
                    "Retrying in {Delay}s.",
                    attempt, MaxRetries, path, RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), cancellationToken)
                          .ConfigureAwait(false);
            }
        }

        // All retries exhausted — log the error then let ReadResponseAsync raise DutchieApiException.
        _logger.LogError(
            "Dutchie API continued returning 500 for {Path} after {MaxRetries} retries. Giving up.",
            path, MaxRetries);

        return await ReadResponseAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new DutchieApiException(
                response.StatusCode,
                body,
                $"Dutchie API returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        return JsonConvert.DeserializeObject<T>(body, JsonSettings)
            ?? throw new DutchieApiException(HttpStatusCode.OK, body, "Dutchie API returned a null or empty response.");
    }

    protected static string BuildQueryString(Action<NameValueCollection> configure)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        configure(query);
        var qs = query.ToString();
        return string.IsNullOrEmpty(qs) ? string.Empty : "?" + qs;
    }

    protected static void Add(NameValueCollection query, string key, DateTimeOffset? value)
    {
        if (value.HasValue)
            query[key] = value.Value.UtcDateTime.ToString("O");
    }

    protected static void Add(NameValueCollection query, string key, bool? value)
    {
        if (value.HasValue)
            query[key] = value.Value ? "true" : "false";
    }

    protected static void Add(NameValueCollection query, string key, int? value)
    {
        if (value.HasValue)
            query[key] = value.Value.ToString();
    }
}
