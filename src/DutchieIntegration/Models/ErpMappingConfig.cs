namespace Dutchie.Integration.Models;

/// <summary>
/// ERP-neutral field-mapping configuration that drives the translation from
/// Dutchie data to ERP documents. Populated by an <see cref="Abstractions.IErpConfigProvider"/>
/// implementation (e.g. from appsettings.json or a Platform Application).
/// </summary>
public sealed class ErpMappingConfig
{
    // ── Process log linking ───────────────────────────────────────────────────

    /// <summary>
    /// RECORDNO of the <c>dutchie_location_config</c> record in Intacct.
    /// Used to link <c>dutchie_process_log</c> entries to the location config after each sync run.
    /// Populated by <c>PlatformAppErpConfigProvider</c>; null when using appsettings config.
    /// </summary>
    public string? LocationConfigRecordNo { get; init; }

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

    /// <summary>
    /// GL account for the rounding / over-short final adjustment entry.
    /// When set, the closing-report pipeline will add a balancing line to ensure
    /// the journal entry debits and credits net to zero.
    /// </summary>
    public string? RoundingAccount { get; init; }

    // ── Category & customer-type summary mappings ─────────────────────────────

    /// <summary>
    /// Maps a Dutchie product category name (e.g. "Flower", "Edibles") to a GL entry spec.
    /// An empty-string key ("") acts as the default fallback for unmapped categories.
    /// </summary>
    public Dictionary<string, SummaryLineConfig> CategoryAccountMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a Dutchie customer type name (e.g. "Recreational", "Medical") to a GL entry spec.
    /// An empty-string key ("") acts as the default fallback for unmapped customer types.
    /// </summary>
    public Dictionary<string, SummaryLineConfig> CustomerTypeAccountMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Live / draft control ──────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, journal entries are posted immediately (live mode).
    /// When <see langword="false"/>, they are held as Draft for manual review before posting.
    /// Sourced from <c>dutchie_master_config.is_live</c> in the Platform App config.
    /// </summary>
    public bool IsLive { get; init; } = true;

    /// <summary>
    /// Maximum acceptable cash over/short discrepancy (absolute $).
    /// Discrepancies within this threshold are absorbed silently; beyond it a warning is raised.
    /// Sourced from <c>dutchie_master_config.maximum_overshort</c>. Defaults to 1.00.
    /// </summary>
    public decimal MaximumOverShort { get; init; } = 1m;

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
