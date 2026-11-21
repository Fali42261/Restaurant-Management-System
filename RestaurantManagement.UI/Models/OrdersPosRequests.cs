namespace RestaurantManagement.UI.Models;

/// <summary>Request payloads sent by the UI to the Orders and POS API.</summary>
public sealed record PosCheckoutRequest(
    string? CustomerName,
    string OrderType,
    Guid? TableId,
    decimal Discount,
    decimal Tax,
    string PaymentMethod,
    decimal CashReceived,
    List<PosCheckoutItem> Items);

public sealed record PosCheckoutItem(Guid ProductId, int Quantity, string? Notes);
public sealed record ReplaceOrderItem(Guid ProductId, int Quantity, string? Notes = null);
public sealed record CreateBillApiRequest(
    Guid? OrderId,
    string CustomerName,
    string OrderType,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total,
    string PaymentMethod,
    decimal AmountPaid);
