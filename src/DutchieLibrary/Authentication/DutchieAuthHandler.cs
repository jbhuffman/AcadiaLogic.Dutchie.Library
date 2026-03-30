using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace Dutchie.Authentication;

internal sealed class DutchieAuthHandler : DelegatingHandler
{
    private readonly DutchieClientOptions _options;

    public DutchieAuthHandler(IOptions<DutchieClientOptions> options)
    {
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var credentials = $"{_options.LocationKey}:{_options.IntegratorKey ?? string.Empty}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        return base.SendAsync(request, cancellationToken);
    }
}
