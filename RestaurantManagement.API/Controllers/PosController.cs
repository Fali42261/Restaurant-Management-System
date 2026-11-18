using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1/pos")]
public sealed class PosController(SqlDataAccess db) : ApiControllerBase(db)
{
    private static string? Normalize(string? value, string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = allowed.FirstOrDefault(x => x.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;
        return value.Trim().Replace(" ", "").Replace("-", "") switch
        {
            "Dinein" => "DineIn",
            "Takeaway" => "Takeaway",
            _ => null
        };
    }

    [HttpPut("bills/{id:guid}/pay")]
    public async Task<IActionResult> MarkBillPaid(Guid id)
    {
        var rows = await Db.QueryProcedureAsync("dbo.usp_Bill_MarkPaid", P("@BillId", id));
        if (rows.Count == 0 || Convert.ToInt32(rows[0]["Updated"]) == 0)
        {
            var exists = await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Bills WHERE BillId=@Id", P("@Id", id));
            return Convert.ToInt32(exists) == 0 ? NotFound() : Success(null, "Bill is already paid.");
        }
        return Success(null, "Bill marked as paid.");
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(PosCheckoutRequest request)
    {
        if (request.Items is null || request.Items.Count is < 1 or > 100 ||
            request.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity is < 1 or > 1000 || x.Notes?.Length > 300))
            return BadRequest(new { success = false, message = "Add 1 to 100 valid product lines. Quantity must be from 1 to 1000." });

        var type = Normalize(request.OrderType, new[] { "DineIn", "Takeaway", "Delivery" });
        var payment = Normalize(request.PaymentMethod, new[] { "Cash", "UPI", "Card", "Online", "Unpaid" });
        if (type is null || payment is null)
            return BadRequest(new { success = false, message = "Select a valid order type and payment method." });
        if (request.CustomerName?.Length > 150 || request.Discount < 0 || request.Tax < 0 || request.CashReceived < 0)
            return BadRequest(new { success = false, message = "Customer name, discount, tax or cash amount is invalid." });
        if (type == "DineIn" && !request.TableId.HasValue)
            return BadRequest(new { success = false, message = "Select a table for dine-in orders." });

        var id = Guid.NewGuid();
        var orderNumber = "ORD-" + id.ToString("N")[..16].ToUpperInvariant();
        var invoiceNumber = "INV-" + id.ToString("N").ToUpperInvariant();
        var itemsJson = JsonSerializer.Serialize(request.Items.Select(x => new { x.ProductId, x.Quantity, x.Notes }));
        var rows = await Db.QueryProcedureAsync("dbo.usp_Pos_Checkout",
            P("@OrderId", id), P("@OrderNumber", orderNumber), P("@InvoiceNumber", invoiceNumber),
            P("@CustomerName", request.CustomerName?.Trim()), P("@OrderType", type), P("@TableId", request.TableId),
            P("@Discount", request.Discount), P("@Tax", request.Tax), P("@PaymentMethod", payment),
            P("@CashReceived", request.CashReceived), P("@CreatedBy", CurrentUserId), P("@ItemsJson", itemsJson));

        if (rows.Count == 0)
            return StatusCode(500, new { success = false, message = "Checkout did not return a saved invoice." });
        return CreatedResult(rows[0], "Order, kitchen ticket and invoice saved successfully.");
    }
}

public sealed record PosCheckoutRequest(
    string? CustomerName,
    string OrderType,
    Guid? TableId,
    decimal Discount,
    decimal Tax,
    string PaymentMethod,
    decimal CashReceived,
    List<PosCheckoutItemRequest> Items);

public sealed record PosCheckoutItemRequest(Guid ProductId, int Quantity, string? Notes);

