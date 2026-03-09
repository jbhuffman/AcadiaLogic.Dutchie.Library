namespace AcadiaLogic.Dutchie;

public sealed class DutchieClientOptions
{
    public const string SectionName = "Dutchie";
    public const string DefaultBaseUrl = "https://api.pos.dutchie.com";

    /// <summary>The dispensary location key (used as HTTP Basic Auth username).</summary>
    public string LocationKey { get; set; } = string.Empty;

    /// <summary>The integrator key (used as HTTP Basic Auth password). Currently optional; will be required in a future release.</summary>
    public string? IntegratorKey { get; set; }

    /// <summary>Base URL for the Dutchie POS API. Defaults to https://api.pos.dutchie.com</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
