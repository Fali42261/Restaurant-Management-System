using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1")]
public sealed class RestaurantController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("floors")] public async Task<IActionResult> Floors() => Success(await Db.QueryAsync("SELECT FloorId,Name,IsActive FROM dbo.Floors WHERE IsDeleted=0 ORDER BY Name"));
    [HttpPost("floors"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> AddFloor(CreateFloorRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.Floors(FloorId,Name,IsActive,IsDeleted) VALUES(@Id,@Name,1,0)",P("@Id",id),P("@Name",r.Name)); return CreatedResult(new { floorId=id }); }
    [HttpGet("tables")] public async Task<IActionResult> Tables() => Success(await Db.QueryAsync("SELECT t.TableId,t.Name,t.Seats,t.Status,t.FloorId,f.Name AS FloorName FROM dbo.RestaurantTables t LEFT JOIN dbo.Floors f ON f.FloorId=t.FloorId WHERE t.IsDeleted=0 ORDER BY f.Name,t.Name"));
    [HttpPost("tables"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> AddTable(CreateTableRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.RestaurantTables(TableId,FloorId,Name,Seats,Status,IsDeleted) VALUES(@Id,@Floor,@Name,@Seats,'Available',0)",P("@Id",id),P("@Floor",r.FloorId),P("@Name",r.Name),P("@Seats",r.Seats)); return CreatedResult(new { tableId=id }); }
    [HttpPut("tables/{id:guid}/status")] public async Task<IActionResult> TableStatus(Guid id, UpdateStatusRequest r) { var n=await Db.ExecuteAsync("UPDATE dbo.RestaurantTables SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE TableId=@Id AND IsDeleted=0",P("@Status",r.Status),P("@Id",id)); return n==0?NotFound():Success(null,"Table status updated."); }

    [HttpGet("orders")] public async Task<IActionResult> Orders([FromQuery] string? status) => Success(await Db.QueryAsync("SELECT OrderId,OrderNumber,CustomerName,OrderType,TableId,Subtotal,Discount,Tax,Total,PaymentMethod,Status,CreatedAt FROM dbo.Orders WHERE IsDeleted=0 AND (@Status IS NULL OR Status=@Status) ORDER BY CreatedAt DESC",P("@Status",status)));
    [HttpPost("orders")] public async Task<IActionResult> CreateOrder(CreateOrderRequest r)
    {
        if(r.Items.Count==0) return BadRequest(new {success=false,message="Add at least one item."});
        var id=Guid.NewGuid(); var number="ORD-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var subtotal=r.Items.Sum(x=>x.Quantity*x.UnitPrice); var total=subtotal-r.Discount+r.Tax;
        await Db.ExecuteAsync("INSERT dbo.Orders(OrderId,OrderNumber,CustomerName,OrderType,TableId,Subtotal,Discount,Tax,Total,PaymentMethod,CashReceived,Status,CreatedBy,CreatedAt,IsDeleted) VALUES(@Id,@No,@Customer,@Type,@Table,@Sub,@Discount,@Tax,@Total,@Payment,@Cash,'Placed',@User,SYSUTCDATETIME(),0)",
            P("@Id",id),P("@No",number),P("@Customer",r.CustomerName),P("@Type",r.OrderType),P("@Table",r.TableId),P("@Sub",subtotal),P("@Discount",r.Discount),P("@Tax",r.Tax),P("@Total",total),P("@Payment",r.PaymentMethod),P("@Cash",r.CashReceived),P("@User",CurrentUserId));
        foreach(var item in r.Items) await Db.ExecuteAsync("INSERT dbo.OrderItems(OrderItemId,OrderId,ProductId,Quantity,UnitPrice,Notes) VALUES(NEWID(),@Order,@Product,@Qty,@Price,@Notes)",P("@Order",id),P("@Product",item.ProductId),P("@Qty",item.Quantity),P("@Price",item.UnitPrice),P("@Notes",item.Notes));
        await Db.ExecuteAsync("INSERT dbo.KitchenTickets(TicketId,OrderId,Status,CreatedAt) VALUES(NEWID(),@Order,'New',SYSUTCDATETIME())",P("@Order",id));
        var invoice = "INV-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var paid = r.PaymentMethod.Equals("Cash",StringComparison.OrdinalIgnoreCase) ? Math.Min(r.CashReceived,total) :
                   r.PaymentMethod.Equals("UPI",StringComparison.OrdinalIgnoreCase) || r.PaymentMethod.Equals("Card",StringComparison.OrdinalIgnoreCase) ? total : 0m;
        await Db.ExecuteAsync("INSERT dbo.Bills(BillId,InvoiceNumber,OrderId,CustomerName,OrderType,Subtotal,Discount,Tax,Total,PaymentMethod,AmountPaid,Status,CreatedAt) VALUES(NEWID(),@Invoice,@Order,@Customer,@Type,@Sub,@Discount,@Tax,@Total,@Method,@Paid,@Status,SYSUTCDATETIME())",
            P("@Invoice",invoice),P("@Order",id),P("@Customer",r.CustomerName ?? "Walk-in Guest"),P("@Type",r.OrderType),P("@Sub",subtotal),P("@Discount",r.Discount),P("@Tax",r.Tax),P("@Total",total),P("@Method",r.PaymentMethod),P("@Paid",paid),P("@Status",paid>=total?"Paid":"Unpaid"));
        return CreatedResult(new { orderId=id,orderNumber=number,invoiceNumber=invoice,total });
    }
    [HttpPut("orders/{id:guid}/status")] public async Task<IActionResult> OrderStatus(Guid id, UpdateStatusRequest r) { var n=await Db.ExecuteAsync("UPDATE dbo.Orders SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE OrderId=@Id AND IsDeleted=0",P("@Status",r.Status),P("@Id",id)); return n==0?NotFound():Success(null,"Order status updated."); }

    [HttpGet("reservations")] public async Task<IActionResult> Reservations() => Success(await Db.QueryAsync("SELECT ReservationId,GuestName,MobileNumber,ReservationDate,ReservationTime,GuestCount,TableId,Status,Notes FROM dbo.Reservations WHERE IsDeleted=0 ORDER BY ReservationDate,ReservationTime"));
    [HttpPost("reservations")] public async Task<IActionResult> AddReservation(CreateReservationRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.Reservations(ReservationId,GuestName,MobileNumber,ReservationDate,ReservationTime,GuestCount,TableId,Status,Notes,CreatedAt,IsDeleted) VALUES(@Id,@Guest,@Mobile,@Date,@Time,@Count,@Table,'Pending',@Notes,SYSUTCDATETIME(),0)",P("@Id",id),P("@Guest",r.GuestName),P("@Mobile",r.MobileNumber),P("@Date",r.ReservationDate.Date),P("@Time",r.ReservationTime),P("@Count",r.GuestCount),P("@Table",r.TableId),P("@Notes",r.Notes)); return CreatedResult(new { reservationId=id }); }
    [HttpPut("reservations/{id:guid}/status")] public async Task<IActionResult> ReservationStatus(Guid id, UpdateStatusRequest r) { var n=await Db.ExecuteAsync("UPDATE dbo.Reservations SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE ReservationId=@Id AND IsDeleted=0",P("@Status",r.Status),P("@Id",id)); return n==0?NotFound():Success(null,"Reservation updated."); }

    [HttpGet("kots")] public async Task<IActionResult> Kots() => Success(await Db.QueryAsync("SELECT k.TicketId,k.OrderId,o.OrderNumber,o.OrderType,o.TableId,k.Status,k.CreatedAt FROM dbo.KitchenTickets k INNER JOIN dbo.Orders o ON o.OrderId=k.OrderId ORDER BY k.CreatedAt"));
    [HttpPut("kots/{id:guid}/status")] public async Task<IActionResult> KotStatus(Guid id, UpdateStatusRequest r) { var n=await Db.ExecuteAsync("UPDATE dbo.KitchenTickets SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE TicketId=@Id",P("@Status",r.Status),P("@Id",id)); return n==0?NotFound():Success(null,"Kitchen ticket updated."); }

    [HttpGet("bills")] public async Task<IActionResult> Bills() => Success(await Db.QueryAsync("SELECT BillId,InvoiceNumber,OrderId,CustomerName,OrderType,Subtotal,Discount,Tax,Total,PaymentMethod,AmountPaid,Status,CreatedAt FROM dbo.Bills ORDER BY CreatedAt DESC"));
    [HttpPost("bills")] public async Task<IActionResult> AddBill(CreateBillRequest r) { var id=Guid.NewGuid(); var invoice="INV-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"); await Db.ExecuteAsync("INSERT dbo.Bills(BillId,InvoiceNumber,OrderId,CustomerName,OrderType,Subtotal,Discount,Tax,Total,PaymentMethod,AmountPaid,Status,CreatedAt) VALUES(@Id,@Invoice,@Order,@Customer,@Type,@Sub,@Discount,@Tax,@Total,@Method,@Paid,@Status,SYSUTCDATETIME())",P("@Id",id),P("@Invoice",invoice),P("@Order",r.OrderId),P("@Customer",r.CustomerName),P("@Type",r.OrderType),P("@Sub",r.Subtotal),P("@Discount",r.Discount),P("@Tax",r.Tax),P("@Total",r.Total),P("@Method",r.PaymentMethod),P("@Paid",r.AmountPaid),P("@Status",r.AmountPaid>=r.Total?"Paid":"Unpaid")); return CreatedResult(new { billId=id,invoiceNumber=invoice }); }
    [HttpPut("bills/{id:guid}/pay")] public async Task<IActionResult> MarkBillPaid(Guid id) { var n=await Db.ExecuteAsync("UPDATE dbo.Bills SET AmountPaid=Total,Status='Paid' WHERE BillId=@Id",P("@Id",id)); return n==0?NotFound():Success(null,"Bill marked paid."); }
}