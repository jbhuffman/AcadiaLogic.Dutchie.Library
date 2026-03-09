using AcadiaLogic.Dutchie.Authentication;
using AcadiaLogic.Dutchie.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie;

public static class DutchieServiceCollectionExtensions
{
    internal const string ReportingClientName = "Dutchie.Reporting";
    internal const string ProductClientName = "Dutchie.Product";

    /// <summary>
    /// Registers Dutchie POS API clients with the DI container.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddDutchieClient(options =>
    /// {
    ///     options.LocationKey   = "your-location-key";
    ///     options.IntegratorKey = "your-integrator-key";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDutchieClient(
        this IServiceCollection services,
        Action<DutchieClientOptions> configure)
    {
        services.Configure(configure);

        services.AddTransient<DutchieAuthHandler>();

        services
            .AddHttpClient<IReportingClient, ReportingClient>(ReportingClientName, ConfigureHttpClient)
            .AddHttpMessageHandler<DutchieAuthHandler>();

        services
            .AddHttpClient<IProductClient, ProductClient>(ProductClientName, ConfigureHttpClient)
            .AddHttpMessageHandler<DutchieAuthHandler>();

        return services;
    }

    /// <summary>
    /// Registers Dutchie POS API clients, binding options from configuration (e.g. appsettings.json section "Dutchie").
    /// </summary>
    public static IServiceCollection AddDutchieClient(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string sectionName = DutchieClientOptions.SectionName)
    {
        services.Configure<DutchieClientOptions>(configuration.GetSection(sectionName));

        services.AddTransient<DutchieAuthHandler>();

        services
            .AddHttpClient<IReportingClient, ReportingClient>(ReportingClientName, ConfigureHttpClient)
            .AddHttpMessageHandler<DutchieAuthHandler>();

        services
            .AddHttpClient<IProductClient, ProductClient>(ProductClientName, ConfigureHttpClient)
            .AddHttpMessageHandler<DutchieAuthHandler>();

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient client)
    {
        var options = sp.GetRequiredService<IOptions<DutchieClientOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }
}
