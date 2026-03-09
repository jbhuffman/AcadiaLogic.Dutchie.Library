using AcadiaLogic.Dutchie.Integration.Models;

namespace AcadiaLogic.Dutchie.Integration.Abstractions;

/// <summary>
/// Loads ERP field-mapping configuration (GL account codes, dimensions, etc.).
/// Implement this interface to source config from appsettings, a Platform Application,
/// a database, or any other store.
/// </summary>
public interface IErpConfigProvider
{
    /// <summary>
    /// Loads the current ERP mapping configuration.
    /// Implementations may cache internally; callers should not cache the result.
    /// </summary>
    Task<ErpMappingConfig> GetConfigAsync(CancellationToken cancellationToken = default);
}
