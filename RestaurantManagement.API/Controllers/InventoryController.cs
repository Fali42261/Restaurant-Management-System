using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager,Staff"), Route("api/v1/inventory")]
public sealed class InventoryController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("items")] public async Task<IActionResult> Items() => Success(await Db.QueryAsync("SELECT ItemId,Name,Unit,CurrentStock,ReorderLevel,CostPrice,IsActive FROM dbo.InventoryItems WHERE IsDeleted=0 ORDER BY Name"));
    [HttpPost("items"),Authorize(Roles="Owner,Manager")] public async Task<IActionResult> AddItem(CreateInventoryItemRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.InventoryItems(ItemId,Name,Unit,CurrentStock,ReorderLevel,CostPrice,IsDeleted,IsActive) VALUES(@Id,@Name,@Unit,@Stock,@Reorder,@Cost,0,1)",P("@Id",id),P("@Name",r.Name),P("@Unit",r.Unit),P("@Stock",r.OpeningStock),P("@Reorder",r.ReorderLevel),P("@Cost",r.CostPrice)); return CreatedResult(new { itemId=id }); }
    [HttpPost("movements"),Authorize(Roles="Owner,Manager")] public async Task<IActionResult> Movement(StockMovementRequest r)
    {
        if(r.Quantity<=0 || (r.MovementType!="IN" && r.MovementType!="OUT")) return BadRequest(new {success=false,message="Movement type must be IN or OUT and quantity must be positive."});
        var change=r.MovementType=="IN"?r.Quantity:-r.Quantity;
        var n=await Db.ExecuteAsync("UPDATE dbo.InventoryItems SET CurrentStock=CurrentStock+@Change WHERE ItemId=@Id AND IsDeleted=0 AND CurrentStock+@Change>=0; IF @@ROWCOUNT=1 INSERT dbo.StockMovements(MovementId,ItemId,MovementType,Quantity,Reference,Notes,CreatedBy,CreatedAt) VALUES(NEWID(),@Id,@Type,@Qty,@Reference,@Notes,@User,SYSUTCDATETIME())",P("@Change",change),P("@Id",r.ItemId),P("@Type",r.MovementType),P("@Qty",r.Quantity),P("@Reference",r.Reference),P("@Notes",r.Notes),P("@User",CurrentUserId));
        return n==0?BadRequest(new {success=false,message="Item not found or insufficient stock."}):Success(null,"Stock updated.");
    }
    [HttpGet("movements")] public async Task<IActionResult> Movements() => Success(await Db.QueryAsync("SELECT m.MovementId,m.ItemId,i.Name AS ItemName,m.MovementType,m.Quantity,m.Reference,m.Notes,m.CreatedAt FROM dbo.StockMovements m INNER JOIN dbo.InventoryItems i ON i.ItemId=m.ItemId ORDER BY m.CreatedAt DESC"));
}