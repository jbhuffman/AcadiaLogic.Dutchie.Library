using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dutchie.Models.Reporting;

/// <summary>
/// A full POS or wholesale transaction with line items and payment details.
/// Returned by GET /reporting/transactions.
/// </summary>
public sealed class Transaction
{
    public int TransactionId { get; init; }
    public int CustomerId { get; init; }
    public int EmployeeId { get; init; }
    public DateTimeOffset TransactionDate { get; init; }
    public DateTimeOffset? VoidDate { get; init; }
    public bool IsVoid { get; init; }
    public double Subtotal { get; init; }
    public double TotalDiscount { get; init; }
    public double TotalBeforeTax { get; init; }
    public double Tax { get; init; }
    public double? TipAmount { get; init; }
    public double Total { get; init; }
    public double Paid { get; init; }
    public double ChangeDue { get; init; }
    public int TotalItems { get; init; }
    public string? TerminalName { get; init; }
    public DateTimeOffset? CheckInDate { get; init; }
    public string? InvoiceNumber { get; init; }
    public bool IsTaxInclusive { get; init; }
    /// <summary>Retail, Transfer, or WholesaleOrder.</summary>
    public string? TransactionType { get; init; }
    public double? LoyaltyEarned { get; init; }
    public double? LoyaltySpent { get; init; }
    public IReadOnlyList<TransactionItem>? Items { get; init; }
    public IReadOnlyList<AppliedDiscount>? Discounts { get; init; }
    public DateTimeOffset LastModifiedDateUtc { get; init; }
    public double? CashPaid { get; init; }
    public double? DebitPaid { get; init; }
    public double? ElectronicPaid { get; init; }
    public string? ElectronicPaymentMethod { get; init; }
    public double? CheckPaid { get; init; }
    public double? CreditPaid { get; init; }
    public double? GiftPaid { get; init; }
    public double? MmapPaid { get; init; }
    public double? PrePaymentAmount { get; init; }
    public double? RevenueFeesAndDonations { get; init; }
    public double? NonRevenueFeesAndDonations { get; init; }
    public IReadOnlyList<FeeDonation>? FeesAndDonations { get; init; }
    public IReadOnlyList<TaxSummaryInfo>? TaxSummary { get; init; }
    public int? ReturnOnTransactionId { get; init; }
    public int? AdjustmentForTransactionId { get; init; }
    public string? OrderType { get; init; }
    public bool WasPreOrdered { get; init; }
    public string? OrderSource { get; init; }
    public string? OrderMethod { get; init; }
    public string? InvoiceName { get; init; }
    public bool IsReturn { get; init; }
    public string? AuthCode { get; init; }
    public int CustomerTypeId { get; init; }
    public bool IsMedical { get; init; }
    public IReadOnlyList<int>? OrderIds { get; init; }
    public double TotalCredit { get; init; }
    public string? CompletedByUser { get; init; }
    public int ResponsibleForSaleUserId { get; init; }
    public DateTimeOffset TransactionDateLocalTime { get; init; }
    public DateTimeOffset? EstTimeArrivalLocal { get; init; }
    public DateTimeOffset? EstDeliveryDateLocal { get; init; }
    public string? ReferenceId { get; init; }
    public IReadOnlyList<ManualPayment>? ManualPayments { get; init; }
    public double? ManualPaid { get; init; }
    public IReadOnlyList<IntegratedPayment>? IntegratedPayments { get; init; }
    public double? IntegratedPaid { get; init; }
    public string? GlobalId { get; init; }

    /// <summary>
    /// Captures unknown API fields so integrations can safely consume newly introduced data
    /// before this model is explicitly updated.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; init; }
}

public sealed class TransactionItem
{
    public int TransactionId { get; init; }
    public int ProductId { get; init; }
    public double TotalPrice { get; init; }
    public double Quantity { get; init; }
    public double UnitPrice { get; init; }
    public double? UnitCost { get; init; }
    public string? PackageId { get; init; }
    public string? SourcePackageId { get; init; }
    public double TotalDiscount { get; init; }
    public int InventoryId { get; init; }
    public int UnitId { get; init; }
    public double? UnitWeight { get; init; }
    public string? UnitWeightUnit { get; init; }
    public double? FlowerEquivalent { get; init; }
    public string? FlowerEquivalentUnit { get; init; }
    public IReadOnlyList<AppliedDiscount>? Discounts { get; init; }
    public IReadOnlyList<int>? QualifiedForDiscountIds { get; init; }
    public IReadOnlyList<LineItemTaxInfo>? Taxes { get; init; }
    public DateTimeOffset? ReturnDate { get; init; }
    public bool IsReturned { get; init; }
    public int? ReturnedByTransactionId { get; init; }
    public string? ReturnReason { get; init; }
    public string? BatchName { get; init; }
    public int TransactionItemId { get; init; }
    public string? Vendor { get; init; }
    public bool IsCoupon { get; init; }
}

public sealed class AppliedDiscount
{
    public int DiscountId { get; init; }
    public string? DiscountName { get; init; }
    public string? DiscountReason { get; init; }
    public double Amount { get; init; }
    public int TransactionItemId { get; init; }
}

public sealed class LineItemTaxInfo
{
    public string? RateName { get; init; }
    public double Rate { get; init; }
    public double Amount { get; init; }
    public int TransactionItemId { get; init; }
}

public sealed class TaxSummaryInfo
{
    public string? RateName { get; init; }
    public double Amount { get; init; }
}

public sealed class IntegratedPayment
{
    public string? IntegrationType { get; init; }
    public double IntegratedPaid { get; init; }
    public string? ExternalPaymentId { get; init; }
}

public sealed class ManualPayment
{
    public string? ManualPaymentProcessorName { get; init; }
    public double ManualPaid { get; init; }
}
