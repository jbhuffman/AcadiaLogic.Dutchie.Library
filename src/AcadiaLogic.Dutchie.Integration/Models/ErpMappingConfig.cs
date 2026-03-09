namespace AcadiaLogic.Dutchie.Integration.Models;

/// <summary>
/// ERP-neutral field-mapping configuration that drives the translation from
/// Dutchie data to ERP documents. Populated by an <see cref="Abstractions.IErpConfigProvider"/>
/// implementation (e.g. from appsettings.json or a Platform Application).
/// </summary>
public sealed class ErpMappingConfig
{
    // ── Journal Entry settings ────────────────────────────────────────────────

    /// <summary>Intacct journal symbol (e.g. "GJ", "APJ"). Required for journal entries.</summary>
    public string JournalSymbol { get; init; } = "GJ";

    /// <summary>Intacct location ID to stamp on every journal line.</summary>
    public string? LocationId { get; init; }

    /// <summary>Intacct department ID to stamp on every journal line.</summary>
    public string? DepartmentId { get; init; }

    /// <summary>Default Intacct item ID for AR invoice lines (from <c>dutchie_master_config</c>).</summary>
    public string? DefaultItemId { get; init; }

    // ── Dutchie API credentials (sourced from dutchie_master_config when using Platform App config) ──

    /// <summary>
    /// Dutchie LocationKey (Basic Auth username) for this location.
    /// Populated when credentials are stored in Intacct rather than environment variables.
    /// When set, the sync pipeline should use these credentials in preference to any
    /// statically-configured <c>DutchieOptions</c>.
    /// </summary>
    public string? DutchieLocationKey { get; init; }

    /// <summary>
    /// Dutchie IntegratorKey (Basic Auth password) for this location.
    /// </summary>
    public string? DutchieIntegratorKey { get; init; }

    /// <summary>
    /// Maps a Dutchie payment type label (e.g. "Cash", "Credit Card", "CanPay")
    /// to an ERP GL account number for the debit side of closing-report journal entries.
    /// </summary>
    public Dictionary<string, string> PaymentTypeAccountMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>GL account credited for gross cannabis sales.</summary>
    public string CannabisSalesAccount { get; init; } = string.Empty;

    /// <summary>GL account credited for non-cannabis sales.</summary>
    public string NonCannabisSalesAccount { get; init; } = string.Empty;

    /// <summary>GL account debited for sales discounts.</summary>
    public string DiscountAccount { get; init; } = string.Empty;

    /// <summary>GL account credited for tax collected (per-rate overrides via <see cref="TaxRateAccountMap"/>).</summary>
    public string DefaultTaxAccount { get; init; } = string.Empty;

    /// <summary>Per-tax-rate GL account overrides (tax rate name → account number).</summary>
    public Dictionary<string, string> TaxRateAccountMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>GL account for tips collected.</summary>
    public string? TipsAccount { get; init; }

    /// <summary>GL account for fees and donations.</summary>
    public string? FeesAccount { get; init; }

    // ── Sales transaction settings ────────────────────────────────────────────

    /// <summary>
    /// Intacct customer ID to use when the Dutchie customer has no matching ERP record
    /// (e.g. a generic "Walk-in Customer" customer).
    /// </summary>
    public string DefaultCustomerId { get; init; } = string.Empty;

    /// <summary>
    /// Maps a Dutchie payment method label to an Intacct payment method / bank account.
    /// Used when posting individual sales transactions.
    /// </summary>
    public Dictionary<string, string> PaymentMethodMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Intacct price list to apply on sales transaction lines.</summary>
    public string? PriceListId { get; init; }
}
