namespace AcadiaLogic.Dutchie.Integration.Models;

/// <summary>
/// ERP-neutral representation of a Dutchie customer sales transaction.
/// The ERP connector translates this into the target system's AR/order-entry document.
/// </summary>
public sealed class SalesTransactionPayload
{
    /// <summary>Dutchie transaction ID — used as an external reference for idempotency.</summary>
    public required int DutchieTransactionId { get; init; }

    public required DateTimeOffset TransactionDate { get; init; }
    public required string ErpCustomerId { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? OrderType { get; init; }
    public string? OrderSource { get; init; }
    public bool IsMedical { get; init; }

    public required IReadOnlyList<SalesLineItem> LineItems { get; init; }
    public required IReadOnlyList<SalesPayment> Payments { get; init; }

    public decimal Subtotal { get; init; }
    public decimal TotalDiscount { get; init; }
    public decimal TotalTax { get; init; }
    public decimal Total { get; init; }

    /// <summary>Optional ERP location ID (for multi-location).</summary>
    public string? LocationId { get; init; }
}

public sealed class SalesLineItem
{
    public required int DutchieProductId { get; init; }
    public string? ProductName { get; init; }
    public string? Sku { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal TotalDiscount { get; init; }
    public decimal TaxAmount { get; init; }
    public string? Unit { get; init; }
}

public sealed class SalesPayment
{
    /// <summary>Dutchie payment method label (e.g. "Cash", "Credit Card", "CanPay").</summary>
    public required string PaymentMethod { get; init; }
    public required decimal Amount { get; init; }
}
