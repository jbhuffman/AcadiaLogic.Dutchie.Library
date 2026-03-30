namespace Dutchie.Intacct.Configuration;

/// <summary>
/// Sage Intacct API credentials and sender configuration.
/// Bind from appsettings.json "Intacct" section, user secrets, or a Platform Application config provider.
/// </summary>
public sealed class IntacctOptions
{
    public const string SectionName = "Intacct";

    /// <summary>Intacct company ID.</summary>
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Intacct user ID (Web Services user).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Intacct user password.</summary>
    public string UserPassword { get; set; } = string.Empty;

    /// <summary>Web Services sender ID (assigned by Sage for your platform app or integration).</summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>Web Services sender password.</summary>
    public string SenderPassword { get; set; } = string.Empty;

    /// <summary>
    /// Intacct entity ID for multi-entity companies. Leave empty for top-level company access.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Intacct Location ID used to filter Platform Application config records (dutchie_config,
    /// dutchie_master_config) to this location. Required when using <c>UsePlatformAppConfig()</c>
    /// in a multi-location Intacct company. Leave empty to load config for all locations.
    /// </summary>
    public string? LocationId { get; set; }
}
