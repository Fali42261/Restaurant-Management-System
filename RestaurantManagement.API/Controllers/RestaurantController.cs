using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1")]
public sealed class RestaurantController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("floors")] public async Task<IActionResult> Floors() => Success(await Db.QueryAsync("SELECT FloorId,Name,IsActive FROM dbo.Floors WHERE IsDeleted=0 ORDER BY Name"));
    [HttpPost("floors"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> AddFloor(CreateFloorRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>100) return BadRequest(new {success=false,message="Floor name is required and must be 100 characters or fewer."});
        var name=r.Name.Trim();
        var exists=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Floors WHERE Name=@Name AND IsDeleted=0",P("@Name",name));
        if(Convert.ToInt32(exists)>0) return Conflict(new {success=false,message="A floor with this name already exists."});
        var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.Floors(FloorId,Name,IsActive,IsDeleted) VALUES(@Id,@Name,1,0)",P("@Id",id),P("@Name",name));
        return CreatedResult(new {floorId=id});
    }
    [HttpPut("floors/{id:guid}"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> UpdateFloor(Guid id, UpdateFloorRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>100) return BadRequest(new {success=false,message="Invalid floor name."});
        var name=r.Name.Trim(); var exists=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Floors WHERE Name=@Name AND FloorId<>@Id AND IsDeleted=0",P("@Name",name),P("@Id",id));
        if(Convert.ToInt32(exists)>0) return Conflict(new {success=false,message="A floor with this name already exists."});
        var n=await Db.ExecuteAsync("UPDATE dbo.Floors SET Name=@Name WHERE FloorId=@Id AND IsDeleted=0",P("@Name",name),P("@Id",id));
        return n==0?NotFound():Success(null,"Floor updated.");
    }
    [HttpDelete("floors/{id:guid}"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> DeleteFloor(Guid id) { var used=Convert.ToInt32(await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.RestaurantTables WHERE FloorId=@Id AND IsDeleted=0",P("@Id",id))); if(used>0) return BadRequest(new {success=false,message="Move or delete the floor tables first."}); var n=await Db.ExecuteAsync("UPDATE dbo.Floors SET IsDeleted=1,IsActive=0 WHERE FloorId=@Id",P("@Id",id)); return n==0?NotFound():Success(null,"Floor deleted."); }
    [HttpGet("tables")] public async Task<IActionResult> Tables() => Success(await Db.QueryAsync("SELECT t.TableId,t.Name,t.Seats,t.Status,t.FloorId,f.Name AS FloorName FROM dbo.RestaurantTables t LEFT JOIN dbo.Floors f ON f.FloorId=t.FloorId WHERE t.IsDeleted=0 ORDER BY f.Name,t.Name"));
    [HttpPost("tables"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> AddTable(CreateTableRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>50||r.Seats<1||r.Seats>100) return BadRequest(new {success=false,message="Table name is required and seats must be between 1 and 100."});
        var floor=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Floors WHERE FloorId=@Floor AND IsDeleted=0 AND IsActive=1",P("@Floor",r.FloorId));
        if(Convert.ToInt32(floor)==0) return BadRequest(new {success=false,message="Selected floor does not exist in this restaurant."});
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.RestaurantTables WHERE FloorId=@Floor AND Name=@Name AND IsDeleted=0",P("@Floor",r.FloorId),P("@Name",r.Name.Trim()));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="A table with this name already exists on the selected floor."});
        var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.RestaurantTables(TableId,FloorId,Name,Seats,Status,IsDeleted) VALUES(@Id,@Floor,@Name,@Seats,'Available',0)",P("@Id",id),P("@Floor",r.FloorId),P("@Name",r.Name.Trim()),P("@Seats",r.Seats));
        return CreatedResult(new {tableId=id});
    }
    [HttpPut("tables/{id:guid}"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> UpdateTable(Guid id, UpdateTableRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>50||r.Seats<1||r.Seats>100) return BadRequest(new {success=false,message="Invalid table name or seats. Seats must be between 1 and 100."});
        var floor=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Floors WHERE FloorId=@Floor AND IsDeleted=0 AND IsActive=1",P("@Floor",r.FloorId));
        if(Convert.ToInt32(floor)==0) return BadRequest(new {success=false,message="Selected floor does not exist in this restaurant."});
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.RestaurantTables WHERE FloorId=@Floor AND Name=@Name AND TableId<>@Id AND IsDeleted=0",P("@Floor",r.FloorId),P("@Name",r.Name.Trim()),P("@Id",id));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="A table with this name already exists on the selected floor."});
        var n=await Db.ExecuteAsync("UPDATE dbo.RestaurantTables SET Name=@Name,Seats=@Seats,FloorId=@Floor,UpdatedAt=SYSUTCDATETIME() WHERE TableId=@Id AND IsDeleted=0",P("@Name",r.Name.Trim()),P("@Seats",r.Seats),P("@Floor",r.FloorId),P("@Id",id));
        return n==0?NotFound():Success(null,"Table updated.");
    }
    [HttpDelete("tables/{id:guid}"), Authorize(Roles="Owner,Manager")] public async Task<IActionResult> DeleteTable(Guid id)
    {
        var inUse=await Db.ScalarAsync("SELECT (SELECT COUNT(1) FROM dbo.Orders WHERE TableId=@Id AND IsDeleted=0 AND Status NOT IN('Completed','Cancelled'))+(SELECT COUNT(1) FROM dbo.Reservations WHERE TableId=@Id AND IsDeleted=0 AND Status IN('Pending','Confirmed'))",P("@Id",id));
        if(Convert.ToInt32(inUse)>0) return Conflict(new {success=false,message="Table is linked to an active order or reservation."});
        var n=await Db.ExecuteAsync("UPDATE dbo.RestaurantTables SET IsDeleted=1 WHERE TableId=@Id AND IsDeleted=0",P("@Id",id));
        return n==0?NotFound():Success(null,"Table deleted.");
    }
    [HttpPut("tables/{id:guid}/status")] public async Task<IActionResult> TableStatus(Guid id, UpdateStatusRequest r)
    {
        var allowed=new[]{"Available","Occupied","Reserved","Cleaning","OutOfService"};
        if(!allowed.Contains(r.Status,StringComparer.OrdinalIgnoreCase)) return BadRequest(new {success=false,message="Invalid table status."});
        var n=await Db.ExecuteAsync("UPDATE dbo.RestaurantTables SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE TableId=@Id AND IsDeleted=0",P("@Status",allowed.First(x=>x.Equals(r.Status,StringComparison.OrdinalIgnoreCase))),P("@Id",id));
        return n==0?NotFound():Success(null,"Table status updated.");
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT OrderId,OrderNumber,CustomerName,OrderType,TableId,Subtotal,Discount,Tax,Total,PaymentMethod,CashReceived,Status,CreatedAt FROM dbo.Orders WHERE OrderId=@Id AND IsDeleted=0",P("@Id",id));
        if(rows.Count==0) return NotFound();
        var items=await Db.QueryAsync("SELECT oi.OrderItemId,oi.ProductId,p.Name AS ProductName,oi.Quantity,oi.UnitPrice,oi.Notes FROM dbo.OrderItems oi INNER JOIN dbo.Products p ON p.ProductId=oi.ProductId WHERE oi.OrderId=@Id ORDER BY oi.OrderItemId",P("@Id",id));
        return Success(new {order=rows[0],items});
    }

    [HttpGet("orders")] public async Task<IActionResult> Orders([FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_Orders_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@Status", status));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest r)
    {
        if(r.Items is null||r.Items.Count==0||r.Items.Count>100||r.Items.Any(x=>x.Quantity<1||x.Quantity>1000))
            return BadRequest(new {success=false,message="An order must contain 1 to 100 lines, each with quantity from 1 to 1000."});
        if(string.IsNullOrWhiteSpace(r.OrderType)||!new[]{"DineIn","Dine-In","Dine In","Takeaway","Take Away","Delivery"}.Contains(r.OrderType,StringComparer.OrdinalIgnoreCase))
            return BadRequest(new {success=false,message="Invalid order type."});
        if(string.IsNullOrWhiteSpace(r.PaymentMethod)||!new[]{"Cash","UPI","Card","Online","Unpaid"}.Contains(r.PaymentMethod,StringComparer.OrdinalIgnoreCase))
            return BadRequest(new {success=false,message="Invalid payment method."});
        if(r.Discount<0||r.Tax<0||r.CashReceived<0)
            return BadRequest(new {success=false,message="Discount, tax and cash received cannot be negative."});
        if(r.TableId.HasValue)
        {
            var table=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.RestaurantTables WHERE TableId=@Id AND IsDeleted=0",P("@Id",r.TableId.Value));
            if(Convert.ToInt32(table)==0) return BadRequest(new {success=false,message="Selected table does not belong to this restaurant or is inactive."});
        }
        var ids=r.Items.Select(x=>x.ProductId).Distinct().ToArray();
        var parameters=ids.Select((id,index)=>P("@Product"+index,id)).ToArray();
        var sql="SELECT ProductId,Price FROM dbo.Products WHERE IsDeleted=0 AND IsAvailable=1 AND ProductId IN ("+string.Join(",",ids.Select((_,index)=>"@Product"+index))+")";
        var products=await Db.QueryAsync(sql,parameters);
        if(products.Count!=ids.Length) return BadRequest(new {success=false,message="One or more products do not exist, are unavailable, or belong to another restaurant."});
        var prices=products.ToDictionary(x=>Guid.Parse(x["ProductId"]!.ToString()!),x=>Convert.ToDecimal(x["Price"]));
        var subtotal=r.Items.Sum(x=>prices[x.ProductId]*x.Quantity);
        if(r.Discount>subtotal) return BadRequest(new {success=false,message="Discount cannot exceed the order subtotal."});
        var total=subtotal-r.Discount+r.Tax;
        var orderId=Guid.NewGuid();
        var number="ORD-"+orderId.ToString("N")[..16].ToUpperInvariant();
        var invoice="INV-"+orderId.ToString("N").ToUpperInvariant();
        var paid=r.PaymentMethod.Equals("Cash",StringComparison.OrdinalIgnoreCase)?Math.Min(r.CashReceived,total):
            r.PaymentMethod.Equals("UPI",StringComparison.OrdinalIgnoreCase)||r.PaymentMethod.Equals("Card",StringComparison.OrdinalIgnoreCase)||r.PaymentMethod.Equals("Online",StringComparison.OrdinalIgnoreCase)?total:0m;
        var billStatus=paid>=total?"Paid":"Unpaid";
        var itemsJson=System.Text.Json.JsonSerializer.Serialize(r.Items.Select(x=>new {x.ProductId,x.Quantity,UnitPrice=prices[x.ProductId],x.Notes}));
        var batch=@"
BEGIN TRANSACTION;
BEGIN TRY
 INSERT dbo.Orders(OrderId,OrderNumber,CustomerName,OrderType,TableId,Subtotal,Discount,Tax,Total,PaymentMethod,CashReceived,Status,CreatedBy,CreatedAt,IsDeleted)
 VALUES(@Id,@No,@Customer,@Type,@Table,@Sub,@Discount,@Tax,@Total,@Payment,@Cash,'Placed',@User,SYSUTCDATETIME(),0);
 INSERT dbo.OrderItems(OrderItemId,OrderId,ProductId,Quantity,UnitPrice,Notes)
 SELECT NEWID(),@Id,j.ProductId,j.Quantity,j.UnitPrice,j.Notes
 FROM OPENJSON(@ItemsJson) WITH(ProductId uniqueidentifier '$.ProductId',Quantity int '$.Quantity',UnitPrice decimal(18,2) '$.UnitPrice',Notes nvarchar(300) '$.Notes') j;
 INSERT dbo.KitchenTickets(TicketId,OrderId,Status,CreatedAt) VALUES(NEWID(),@Id,'New',SYSUTCDATETIME());
 INSERT dbo.Bills(BillId,InvoiceNumber,OrderId,CustomerName,OrderType,Subtotal,Discount,Tax,Total,PaymentMethod,AmountPaid,Status,CreatedAt)
 VALUES(NEWID(),@Invoice,@Id,COALESCE(@Customer,N'Walk-in Guest'),@Type,@Sub,@Discount,@Tax,@Total,@Payment,@Paid,@BillStatus,SYSUTCDATETIME());
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;";
        await Db.ExecuteAsync(batch,P("@Id",orderId),P("@No",number),P("@Customer",r.CustomerName),P("@Type",r.OrderType),P("@Table",r.TableId),
            P("@Sub",subtotal),P("@Discount",r.Discount),P("@Tax",r.Tax),P("@Total",total),P("@Payment",r.PaymentMethod),P("@Cash",r.CashReceived),
            P("@User",CurrentUserId),P("@Invoice",invoice),P("@Paid",paid),P("@BillStatus",billStatus),P("@ItemsJson",itemsJson));
        return CreatedResult(new {orderId,orderNumber=number,invoiceNumber=invoice,subtotal,discount=r.Discount,tax=r.Tax,total,amountPaid=paid,status=billStatus});
    }

    [HttpPut("orders/{id:guid}")]
    public async Task<IActionResult> UpdateOrder(Guid id,UpdateOrderRequest r)
    {
        var orderTypes=new[]{"DineIn","Dine-In","Takeaway","Take Away","Delivery"};
        var statuses=new[]{"Placed","Preparing","Ready","Completed","Cancelled"};
        if(!orderTypes.Contains(r.OrderType,StringComparer.OrdinalIgnoreCase)||!statuses.Contains(r.Status,StringComparer.OrdinalIgnoreCase))
            return BadRequest(new {success=false,message="Invalid order type or status."});
        if(r.CustomerName?.Length>150) return BadRequest(new {success=false,message="Customer name cannot exceed 150 characters."});
        if(r.TableId.HasValue)
        {
            var table=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.RestaurantTables WHERE TableId=@Id AND IsDeleted=0",P("@Id",r.TableId.Value));
            if(Convert.ToInt32(table)==0) return BadRequest(new {success=false,message="Selected table does not belong to this restaurant."});
        }
        var status=statuses.First(x=>x.Equals(r.Status,StringComparison.OrdinalIgnoreCase));
        var n=await Db.ExecuteAsync("UPDATE dbo.Orders SET CustomerName=@Customer,OrderType=@Type,TableId=@Table,Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE OrderId=@Id AND IsDeleted=0",
            P("@Customer",r.CustomerName?.Trim()),P("@Type",r.OrderType),P("@Table",r.TableId),P("@Status",status),P("@Id",id));
        return n==0?NotFound():Success(null,"Order updated.");
    }
    [HttpDelete("orders/{id:guid}")] public async Task<IActionResult> DeleteOrder(Guid id) { var n=await Db.ExecuteAsync("UPDATE dbo.Orders SET IsDeleted=1,UpdatedAt=SYSUTCDATETIME() WHERE OrderId=@Id",P("@Id",id)); return n==0?NotFound():Success(null,"Order deleted."); }
    [HttpPut("orders/{id:guid}/status")] public async Task<IActionResult> OrderStatus(Guid id,UpdateStatusRequest r)
    {
        var allowed=new[]{"Placed","Preparing","Ready","Completed","Cancelled"};
        if(!allowed.Contains(r.Status,StringComparer.OrdinalIgnoreCase)) return BadRequest(new {success=false,message="Invalid order status."});
        var status=allowed.First(x=>x.Equals(r.Status,StringComparison.OrdinalIgnoreCase));
        var n=await Db.ExecuteAsync("UPDATE dbo.Orders SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE OrderId=@Id AND IsDeleted=0",P("@Status",status),P("@Id",id));
        return n==0?NotFound():Success(null,"Order status updated.");
    }

    [HttpGet("reservations/{id:guid}")]
    public async Task<IActionResult> Reservation(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT ReservationId,GuestName,MobileNumber,ReservationDate,ReservationTime,GuestCount,TableId,Status,Notes,CreatedAt FROM dbo.Reservations WHERE ReservationId=@Id AND IsDeleted=0",P("@Id",id));
        return rows.Count==0?NotFound():Success(rows[0]);
    }

    [HttpGet("reservations")] public async Task<IActionResult> Reservations([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] DateTime? date = null, [FromQuery] string? status = null)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_Reservations_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@Date", date?.Date), P("@Status", status));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPost("reservations")]
    public async Task<IActionResult> AddReservation(CreateReservationRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.GuestName)||r.GuestName.Trim().Length>150||string.IsNullOrWhiteSpace(r.MobileNumber)||r.MobileNumber.Trim().Length<10||r.GuestCount<1||r.GuestCount>100||r.ReservationDate.Date>DateTime.UtcNow.Date.AddYears(1)||r.ReservationTime<TimeSpan.Zero||r.ReservationTime>=TimeSpan.FromDays(1))
            return BadRequest(new {success=false,message="Enter valid guest details, guest count and a reservation date within the next year."});
        if(r.TableId.HasValue)
        {
            var tables=await Db.QueryAsync("SELECT Seats FROM dbo.RestaurantTables WHERE TableId=@Id AND IsDeleted=0 AND Status<>'OutOfService'",P("@Id",r.TableId.Value));
            if(tables.Count==0) return BadRequest(new {success=false,message="Selected table is not available in this restaurant."});
            if(r.GuestCount>Convert.ToInt32(tables[0]["Seats"])) return BadRequest(new {success=false,message="Guest count exceeds the selected table capacity."});
            var conflict=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Reservations WHERE TableId=@Table AND ReservationDate=@Date AND ABS(DATEDIFF(MINUTE,CAST(ReservationTime AS datetime2),CAST(@Time AS datetime2)))<90 AND IsDeleted=0 AND Status IN('Pending','Confirmed')",P("@Table",r.TableId.Value),P("@Date",r.ReservationDate.Date),P("@Time",r.ReservationTime));
            if(Convert.ToInt32(conflict)>0) return Conflict(new {success=false,message="This table already has a reservation at the selected time."});
        }
        var id=Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.Reservations(ReservationId,GuestName,MobileNumber,ReservationDate,ReservationTime,GuestCount,TableId,Status,Notes,CreatedAt,IsDeleted) VALUES(@Id,@Guest,@Mobile,@Date,@Time,@Count,@Table,'Pending',@Notes,SYSUTCDATETIME(),0)",
            P("@Id",id),P("@Guest",r.GuestName.Trim()),P("@Mobile",r.MobileNumber.Trim()),P("@Date",r.ReservationDate.Date),P("@Time",r.ReservationTime),P("@Count",r.GuestCount),P("@Table",r.TableId),P("@Notes",r.Notes?.Trim()));
        return CreatedResult(new {reservationId=id});
    }
    [HttpPut("reservations/{id:guid}/status")]
    public async Task<IActionResult> ReservationStatus(Guid id,UpdateStatusRequest r)
    {
        var allowed=new[]{"Pending","Confirmed","Cancelled","Seated","NoShow"};
        if(!allowed.Contains(r.Status,StringComparer.OrdinalIgnoreCase)) return BadRequest(new {success=false,message="Invalid reservation status."});
        var status=allowed.First(x=>x.Equals(r.Status,StringComparison.OrdinalIgnoreCase));
        var n=await Db.ExecuteAsync("UPDATE dbo.Reservations SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE ReservationId=@Id AND IsDeleted=0",P("@Status",status),P("@Id",id));
        return n==0?NotFound():Success(null,"Reservation status updated.");
    }
    [HttpDelete("reservations/{id:guid}"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> DeleteReservation(Guid id)
    {
        var n=await Db.ExecuteAsync("UPDATE dbo.Reservations SET IsDeleted=1,UpdatedAt=SYSUTCDATETIME() WHERE ReservationId=@Id AND IsDeleted=0",P("@Id",id));
        return n==0?NotFound():Success(null,"Reservation cancelled.");
    }

    [HttpGet("kots/{id:guid}")]
    public async Task<IActionResult> Kot(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT k.TicketId,k.OrderId,o.OrderNumber,o.OrderType,o.TableId,k.Status,k.CreatedAt,k.UpdatedAt FROM dbo.KitchenTickets k INNER JOIN dbo.Orders o ON o.OrderId=k.OrderId WHERE k.TicketId=@Id",P("@Id",id));
        if(rows.Count==0) return NotFound();
        var items=await Db.QueryAsync("SELECT oi.ProductId,p.Name AS ProductName,oi.Quantity,oi.Notes FROM dbo.OrderItems oi INNER JOIN dbo.Products p ON p.ProductId=oi.ProductId INNER JOIN dbo.KitchenTickets k ON k.OrderId=oi.OrderId WHERE k.TicketId=@Id ORDER BY p.Name",P("@Id",id));
        return Success(new {ticket=rows[0],items});
    }

    [HttpGet("kots")] public async Task<IActionResult> Kots([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] string? status = null)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_KitchenTickets_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@Status", status));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPut("kots/{id:guid}/status")] public async Task<IActionResult> KotStatus(Guid id,UpdateStatusRequest r)
    {
        var allowed=new[]{"New","Preparing","Ready","Served","Cancelled"};
        if(!allowed.Contains(r.Status,StringComparer.OrdinalIgnoreCase)) return BadRequest(new {success=false,message="Invalid kitchen ticket status."});
        var status=allowed.First(x=>x.Equals(r.Status,StringComparison.OrdinalIgnoreCase));
        var n=await Db.ExecuteAsync("UPDATE dbo.KitchenTickets SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE TicketId=@Id",P("@Status",status),P("@Id",id));
        return n==0?NotFound():Success(null,"Kitchen ticket updated.");
    }

    [HttpGet("bills/{id:guid}")]
    public async Task<IActionResult> Bill(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT BillId,InvoiceNumber,OrderId,CustomerName,OrderType,Subtotal,Discount,Tax,Total,PaymentMethod,AmountPaid,Status,CreatedAt FROM dbo.Bills WHERE BillId=@Id",P("@Id",id));
        return rows.Count==0?NotFound():Success(rows[0]);
    }

    [HttpGet("bills")] public async Task<IActionResult> Bills([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_Bills_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@FromUtc", from?.Date), P("@ToUtc", to?.Date.AddDays(1)));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPost("bills")]
    public async Task<IActionResult> AddBill(CreateBillRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.CustomerName)||r.CustomerName.Trim().Length>150||r.Subtotal<0||r.Discount<0||r.Tax<0||r.Total<=0||r.AmountPaid<0||r.AmountPaid>r.Total||r.Discount>r.Subtotal||r.Total!=r.Subtotal-r.Discount+r.Tax||
           !new[]{"DineIn","Dine-In","Dine In","Takeaway","Take Away","Delivery"}.Contains(r.OrderType,StringComparer.OrdinalIgnoreCase)||
           !new[]{"Cash","UPI","Card","Online","Unpaid"}.Contains(r.PaymentMethod,StringComparer.OrdinalIgnoreCase))
            return BadRequest(new {success=false,message="Invalid bill details or totals. Total must equal subtotal minus discount plus tax."});
        if(r.OrderId.HasValue)
        {
            var order=await Db.QueryAsync("SELECT Subtotal,Discount,Tax,Total FROM dbo.Orders WHERE OrderId=@Id AND IsDeleted=0",P("@Id",r.OrderId.Value));
            if(order.Count==0) return BadRequest(new {success=false,message="Linked order does not exist in this restaurant."});
            if(Convert.ToDecimal(order[0]["Subtotal"])!=r.Subtotal||Convert.ToDecimal(order[0]["Discount"])!=r.Discount||Convert.ToDecimal(order[0]["Tax"])!=r.Tax||Convert.ToDecimal(order[0]["Total"])!=r.Total)
                return BadRequest(new {success=false,message="Bill amounts must match the linked order."});
        }
        var id=Guid.NewGuid(); var invoice="INV-"+id.ToString("N").ToUpperInvariant();
        await Db.ExecuteAsync("INSERT dbo.Bills(BillId,InvoiceNumber,OrderId,CustomerName,OrderType,Subtotal,Discount,Tax,Total,PaymentMethod,AmountPaid,Status,CreatedAt) VALUES(@Id,@Invoice,@Order,@Customer,@Type,@Sub,@Discount,@Tax,@Total,@Method,@Paid,@Status,SYSUTCDATETIME())",
            P("@Id",id),P("@Invoice",invoice),P("@Order",r.OrderId),P("@Customer",r.CustomerName.Trim()),P("@Type",r.OrderType),P("@Sub",r.Subtotal),P("@Discount",r.Discount),P("@Tax",r.Tax),P("@Total",r.Total),P("@Method",r.PaymentMethod),P("@Paid",r.AmountPaid),P("@Status",r.AmountPaid>=r.Total?"Paid":"Unpaid"));
        return CreatedResult(new {billId=id,invoiceNumber=invoice});
    }
    [HttpPut("bills/{id:guid}/pay")] public async Task<IActionResult> MarkBillPaid(Guid id) { var n=await Db.ExecuteAsync("UPDATE dbo.Bills SET AmountPaid=Total,Status='Paid' WHERE BillId=@Id",P("@Id",id)); return n==0?NotFound():Success(null,"Bill marked paid."); }
}