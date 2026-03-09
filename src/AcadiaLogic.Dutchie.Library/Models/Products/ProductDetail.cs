namespace AcadiaLogic.Dutchie.Models.Products;

/// <summary>
/// Full product catalog entry with pricing, compliance, and e-commerce metadata.
/// Returned by GET /products.
/// </summary>
public sealed class ProductDetail
{
    public int ProductId { get; init; }
    public string? GlobalProductId { get; init; }
    public string? Sku { get; init; }
    public string? InternalName { get; init; }
    public string? ProductName { get; init; }
    public string? Description { get; init; }
    public string? MasterCategory { get; init; }
    public int? CategoryId { get; init; }
    public string? Category { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<string>? ImageUrls { get; init; }
    public int? StrainId { get; init; }
    public string? Strain { get; init; }
    /// <summary>Hybrid, Indica, Sativa, or CBD.</summary>
    public string? StrainType { get; init; }
    public string? Size { get; init; }
    public double? NetWeight { get; init; }
    public int? NetWeightUnitId { get; init; }
    public string? NetWeightUnit { get; init; }
    public int? BrandId { get; init; }
    public string? BrandName { get; init; }
    public string? LibraryProductId { get; init; }
    public int? VendorId { get; init; }
    public string? VendorName { get; init; }
    public bool IsCannabis { get; init; }
    public bool IsActive { get; init; }
    public bool IsCoupon { get; init; }
    public double? ThcContent { get; init; }
    /// <summary>%, mg, mg/g, or n.d.</summary>
    public string? ThcContentUnit { get; init; }
    public double? CbdContent { get; init; }
    /// <summary>%, mg, mg/g, or n.d.</summary>
    public string? CbdContentUnit { get; init; }
    public double? ProductGrams { get; init; }
    public double? FlowerEquivalent { get; init; }
    public double? RecFlowerEquivalent { get; init; }
    public double? Price { get; init; }
    public double? MedPrice { get; init; }
    public double? RecPrice { get; init; }
    public double? UnitCost { get; init; }
    public string? UnitType { get; init; }
    public string? OnlineTitle { get; init; }
    public string? OnlineDescription { get; init; }
    public bool? OnlineProduct { get; init; }
    public bool? PosProducts { get; init; }
    public int? PricingTier { get; init; }
    public bool? OnlineAvailable { get; init; }
    public double? LowInventoryThreshold { get; init; }
    public string? PricingTierName { get; init; }
    public string? PricingTierDescription { get; init; }
    public IReadOnlyList<PricingTierData>? PricingTierData { get; init; }
    public string? Flavor { get; init; }
    public string? AlternateName { get; init; }
    public string? LineageName { get; init; }
    public string? DistillationName { get; init; }
    public double? MaxPurchaseablePerTransaction { get; init; }
    public IReadOnlyList<ProductTag>? Tags { get; init; }
    public IReadOnlyList<ProductEffect>? Effects { get; init; }
    public string? Dosage { get; init; }
    public string? Instructions { get; init; }
    public string? Allergens { get; init; }
    public StandardAllergensDetails? StandardAllergens { get; init; }
    public string? DefaultUnit { get; init; }
    public int? ProducerId { get; init; }
    public string? ProducerName { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public bool IsMedicalOnly { get; init; }
    public DateTimeOffset? LastModifiedDateUtc { get; init; }
    public double? GrossWeight { get; init; }
    public bool? IsTaxable { get; init; }
    public IReadOnlyList<string>? TaxCategories { get; init; }
    public string? Upc { get; init; }
    public string? RegulatoryCategory { get; init; }
    public string? Ndc { get; init; }
    public double? DaysSupply { get; init; }
    public string? IllinoisTaxCategory { get; init; }
    public string? ExternalCategory { get; init; }
    public string? ExternalId { get; init; }
    public bool SyncExternally { get; init; }
    public string? RegulatoryName { get; init; }
    public string? AdministrationMethod { get; init; }
    public double? UnitCbdContentDose { get; init; }
    public double? UnitThcContentDose { get; init; }
    public double? OilVolume { get; init; }
    public string? IngredientList { get; init; }
    public int? ExpirationDays { get; init; }
    public string? Abbreviation { get; init; }
    public bool IsTestProduct { get; init; }
    public bool IsFinished { get; init; }
    public bool AllowAutomaticDiscounts { get; init; }
    public string? ServingSize { get; init; }
    public int? ServingSizePerUnit { get; init; }
    public bool IsNutrient { get; init; }
    public DateTimeOffset? ApprovalDateUtc { get; init; }
    public string? EcomCategory { get; init; }
    public string? EcomSubcategory { get; init; }
    public IReadOnlyList<string>? EcomSubcategories { get; init; }
    public string? CustomMetadata { get; init; }
}

public sealed class ProductTag
{
    public int TagId { get; init; }
    public string? TagName { get; init; }
    public int ProductId { get; init; }
}

public sealed class ProductEffect
{
    public int EffectId { get; init; }
    public string? EffectName { get; init; }
    public int ProductId { get; init; }
}

public sealed class PricingTierData
{
    public double? StartWeight { get; init; }
    public double? EndWeight { get; init; }
    public double Price { get; init; }
    public double MedicalPrice { get; init; }
}

public sealed class StandardAllergensDetails
{
    public bool Milk { get; init; }
    public bool Eggs { get; init; }
    public bool Fish { get; init; }
    public bool Peanuts { get; init; }
    public bool TreeNuts { get; init; }
    public bool Sesame { get; init; }
    public bool Shellfish { get; init; }
    public bool Soybeans { get; init; }
    public bool Wheat { get; init; }
}
