using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager,Staff"), Route("api/v1/inventory")]
public sealed class InventoryController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("items")] public async Task<IActionResult> Items([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_InventoryItems_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@Search", string.IsNullOrWhiteSpace(search) ? null : search.Trim()));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPost("items"),Authorize(Roles="Owner,Manager")] public async Task<IActionResult> AddItem(CreateInventoryItemRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.InventoryItems(ItemId,Name,Unit,CurrentStock,ReorderLevel,CostPrice,IsDeleted,IsActive) VALUES(@Id,@Name,@Unit,@Stock,@Reorder,@Cost,0,1)",P("@Id",id),P("@Name",r.Name),P("@Unit",r.Unit),P("@Stock",r.OpeningStock),P("@Reorder",r.ReorderLevel),P("@Cost",r.CostPrice)); return CreatedResult(new { itemId=id }); }
    [HttpPost("movements"),Authorize(Roles="Owner,Manager")] public async Task<IActionResult> Movement(StockMovementRequest r)
    {
        if(r.Quantity<=0 || (r.MovementType!="IN" && r.MovementType!="OUT")) return BadRequest(new {success=false,message="Movement type must be IN or OUT and quantity must be positive."});
        var change=r.MovementType=="IN"?r.Quantity:-r.Quantity;
        var n=await Db.ExecuteAsync("UPDATE dbo.InventoryItems SET CurrentStock=CurrentStock+@Change WHERE ItemId=@Id AND IsDeleted=0 AND CurrentStock+@Change>=0; IF @@ROWCOUNT=1 INSERT dbo.StockMovements(MovementId,ItemId,MovementType,Quantity,Reference,Notes,CreatedBy,CreatedAt) VALUES(NEWID(),@Id,@Type,@Qty,@Reference,@Notes,@User,SYSUTCDATETIME())",P("@Change",change),P("@Id",r.ItemId),P("@Type",r.MovementType),P("@Qty",r.Quantity),P("@Reference",r.Reference),P("@Notes",r.Notes),P("@User",CurrentUserId));
        return n==0?BadRequest(new {success=false,message="Item not found or insufficient stock."}):Success(null,"Stock updated.");
    }
    [HttpGet("movements")] public async Task<IActionResult> Movements([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? itemId = null)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_StockMovements_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@ItemId", itemId));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
}