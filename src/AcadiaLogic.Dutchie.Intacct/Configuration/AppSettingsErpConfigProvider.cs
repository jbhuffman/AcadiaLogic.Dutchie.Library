using AcadiaLogic.Dutchie.Integration.Abstractions;
using AcadiaLogic.Dutchie.Integration.Models;
using Microsoft.Extensions.Options;

namespace AcadiaLogic.Dutchie.Intacct.Configuration;

/// <summary>
/// Loads <see cref="ErpMappingConfig"/> from appsettings.json (or any IOptions source).
/// Use this during development or when configuration lives in app settings.
/// For production, replace with a Platform Application-backed provider once credentials are available.
/// </summary>
public sealed class AppSettingsErpConfigProvider : IErpConfigProvider
{
    private readonly ErpMappingConfig _config;

    public AppSettingsErpConfigProvider(IOptions<ErpMappingConfig> options)
    {
        _config = options.Value;
    }

    public Task<ErpMappingConfig> GetConfigAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_config);

    public Task<IReadOnlyList<ErpMappingConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ErpMappingConfig>>([_config]);
}
