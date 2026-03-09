using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using AcadiaLogic.Dutchie.Intacct.Configuration;
using AcadiaLogic.Dutchie.Intacct.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace AcadiaLogic.Dutchie.Intacct;

public static class IntacctServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Sage Intacct ERP connector and the appsettings-backed config provider.
    /// Call after <c>AddDutchieClient(...)</c> and before <c>AddDutchieIntegration(...)</c>.
    /// </summary>
    /// <param name="configureIntacct">Bind Intacct credentials (company, user, sender).</param>
    /// <param name="configureMappings">Bind GL account mappings for journal entries and transactions.</param>
    public static IServiceCollection AddIntacctConnector(
        this IServiceCollection services,
        Action<IntacctOptions> configureIntacct,
        Action<ErpMappingConfig>? configureMappings = null)
    {
        services.Configure(configureIntacct);

        if (configureMappings is not null)
            services.Configure(configureMappings);
        else
            services.Configure<ErpMappingConfig>(_ => { });

        services.AddTransient<IErpConnector, IntacctErpConnector>();
        services.AddTransient<IErpConfigProvider, AppSettingsErpConfigProvider>();

        return services;
    }

    /// <summary>
    /// Variant that binds Intacct credentials from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public static IServiceCollection AddIntacctConnector(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string intacctSection = IntacctOptions.SectionName,
        string mappingSection = "DutchieErpMappings")
    {
        services.Configure<IntacctOptions>(configuration.GetSection(intacctSection));
        services.Configure<ErpMappingConfig>(configuration.GetSection(mappingSection));

        services.AddTransient<IErpConnector, IntacctErpConnector>();
        services.AddTransient<IErpConfigProvider, AppSettingsErpConfigProvider>();

        return services;
    }

    /// <summary>
    /// Swaps the config provider to the Platform Application-backed implementation.
    /// Call this after <see cref="AddIntacctConnector(IServiceCollection, Action{IntacctOptions}, Action{ErpMappingConfig}?)"/>
    /// once Platform Application credentials and schema are available.
    /// </summary>
    public static IServiceCollection UsePlatformAppConfig(this IServiceCollection services)
    {
        // Replace the appsettings provider with the Platform App provider
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IErpConfigProvider));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddTransient<IErpConfigProvider, PlatformAppErpConfigProvider>();
        return services;
    }
}
