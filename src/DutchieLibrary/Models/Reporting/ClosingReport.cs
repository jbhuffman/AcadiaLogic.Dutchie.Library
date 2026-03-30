using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dutchie.Models.Reporting;

/// <summary>
/// Comprehensive financial closing report for a dispensary location.
/// Returned by GET /reporting/closing-report. Date range must be 12 hours to 31 days.
/// </summary>
public sealed class ClosingReport
{
    public double? TotalTips { get; init; }
    public double PayByBankTips { get; init; }
    public double PayByBankTransactionFees { get; init; }
    public IReadOnlyList<ClosingReportPayByBankBatchFileSums>? PayByBankBatchFile { get; init; }
    public IReadOnlyList<FeeDonation>? FeesDonations { get; init; }

    // Backward-compat aliases
    public double? DutchiePayTips { get; init; }
    public double? DutchiePayTotalAdjustmentAmount { get; init; }
    public IReadOnlyList<ClosingReportPayByBankBatchFileSums>? DutchiePayBatchFileSums { get; init; }

    public double? GrossSales { get; init; }
    public double? Discount { get; init; }
    public double? Loyalty { get; init; }
    public double? TotalTax { get; init; }
    public double? Cost { get; init; }
    public double? Coupons { get; init; }
    public double? ItemTotal { get; init; }
    public int TransactionCount { get; init; }
    public int ItemCount { get; init; }
    public int CustomerCount { get; init; }
    public int NewCustomerCount { get; init; }
    public int VoidCount { get; init; }
    public double? VoidTotal { get; init; }
    public double? ReturnTotal { get; init; }
    public double? StartingBalance { get; init; }
    public double? EndingBalance { get; init; }
    public double? Deposits { get; init; }
    public double? Adjustments { get; init; }
    public double? TotalPayments { get; init; }
    public double? InvoiceTotal { get; init; }
    public double? CannabisSales { get; init; }
    public double? NonCannabisSales { get; init; }
    public double? NetSales { get; init; }
    public double? RevenueFeesDonations { get; init; }
    public double? NonRevenueFeesDonations { get; init; }
    public double? Rounding { get; init; }
    public double? TotalIncome { get; init; }
    public double? AverageCartNetSales { get; init; }
    public double? OverShort { get; init; }

    public IReadOnlyList<ClosingReportCategorySummary>? CategorySummary { get; init; }
    public IReadOnlyList<ClosingReportPaymentSummary>? PaymentSummary { get; init; }
    public IReadOnlyList<ClosingReportTaxRateSummary>? TaxSummary { get; init; }
    public IReadOnlyList<ClosingReportCustomerTypeSummary>? CustomerTypeSummary { get; init; }
    public IReadOnlyList<ClosingReportOrderTypeSummary>? OrderTypeSummary { get; init; }
    public IReadOnlyList<ClosingReportOrderSourceSummary>? OrderSourceSummary { get; init; }

    /// <summary>
    /// Captures unknown API fields so integrations can safely consume newly introduced data
    /// before this model is explicitly updated.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; init; }
}

public sealed class ClosingReportCategorySummary
{
    public string? Category { get; init; }
    /// <summary>Calculated alias for CategoryNetTotal.</summary>
    public double CategoryTotal { get; init; }
    public double CategoryGrossTotal { get; init; }
    public double CategoryDiscountTotal { get; init; }
    public double CategoryNetTotal { get; init; }
    public double CategoryCost { get; init; }
}

public sealed class ClosingReportPaymentSummary
{
    public string? PaymentType { get; init; }
    public double TotalPaid { get; init; }
}

public sealed class ClosingReportTaxRateSummary
{
    public string? TaxRate { get; init; }
    public double TotalTax { get; init; }
}

public sealed class ClosingReportCustomerTypeSummary
{
    public string? CustomerType { get; init; }
    /// <summary>Calculated alias for NetTotal.</summary>
    public double Total { get; init; }
    public double GrossTotal { get; init; }
    public double NetTotal { get; init; }
    public double DiscountTotal { get; init; }
    public double CustomerTypeCost { get; init; }
    public double CannabisSales { get; init; }
    public double NonCannabisSales { get; init; }
}

public sealed class ClosingReportOrderTypeSummary
{
    public string? OrderType { get; init; }
    /// <summary>Calculated alias for NetTotal.</summary>
    public double Total { get; init; }
    public double GrossTotal { get; init; }
    public double NetTotal { get; init; }
    public double DiscountTotal { get; init; }
    public double OrderTypeCost { get; init; }
}

public sealed class ClosingReportOrderSourceSummary
{
    public string? OrderSource { get; init; }
    /// <summary>Calculated alias for NetTotal.</summary>
    public double Total { get; init; }
    public double GrossTotal { get; init; }
    public double NetTotal { get; init; }
    public double DiscountTotal { get; init; }
    public double OrderSourceCost { get; init; }
}

public sealed class ClosingReportPayByBankBatchFileSums
{
    public string? BatchFileName { get; init; }
    public double PayByBankBatchFileAdjustmentAmount { get; init; }
}

public sealed class FeeDonation
{
    public string? Name { get; init; }
    public double CashValue { get; init; }
    public bool IsRevenue { get; init; }
}
