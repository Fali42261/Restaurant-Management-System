using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager,Staff"), Route("api/v1/inventory")]
public sealed class InventoryController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery]int pageNumber=1,[FromQuery]int pageSize=25,[FromQuery]string? search=null)
    {
        var page=Math.Max(1,pageNumber); var size=Math.Clamp(pageSize,1,100);
        var rows=await Db.QueryProcedureAsync("dbo.usp_InventoryItems_ListPaged",P("@PageNumber",page),P("@PageSize",size+1),P("@Search",string.IsNullOrWhiteSpace(search)?null:search.Trim()));
        var hasMore=rows.Count>size; if(hasMore) rows.RemoveAt(size);
        return Success(new {items=rows,pageNumber=page,pageSize=size,hasMore});
    }

    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> Item(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT ItemId,Name,Unit,CurrentStock,ReorderLevel,CostPrice,IsActive FROM dbo.InventoryItems WHERE ItemId=@Id AND IsDeleted=0",P("@Id",id));
        return rows.Count==0?NotFound():Success(rows[0]);
    }

    [HttpPost("items"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> AddItem(CreateInventoryItemRequest r)
    {
        var error=Validate(r.Name,r.Unit,r.OpeningStock,r.ReorderLevel,r.CostPrice);
        if(error is not null) return BadRequest(new {success=false,message=error});
        var name=r.Name.Trim();
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.InventoryItems WHERE Name=@Name AND IsDeleted=0",P("@Name",name));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="An inventory item with this name already exists."});
        var id=Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.InventoryItems(ItemId,Name,Unit,CurrentStock,ReorderLevel,CostPrice,IsDeleted,IsActive) VALUES(@Id,@Name,@Unit,@Stock,@Reorder,@Cost,0,1); IF @Stock>0 INSERT dbo.StockMovements(MovementId,ItemId,MovementType,Quantity,Reference,Notes,CreatedBy,CreatedAt) VALUES(NEWID(),@Id,'IN',@Stock,N'Opening Stock',N'Initial stock',@User,SYSUTCDATETIME())",
            P("@Id",id),P("@Name",name),P("@Unit",r.Unit.Trim()),P("@Stock",r.OpeningStock),P("@Reorder",r.ReorderLevel),P("@Cost",r.CostPrice),P("@User",CurrentUserId));
        return CreatedResult(new {itemId=id});
    }

    [HttpPut("items/{id:guid}"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> UpdateItem(Guid id,UpdateInventoryItemRequest r)
    {
        var error=Validate(r.Name,r.Unit,0,r.ReorderLevel,r.CostPrice);
        if(error is not null) return BadRequest(new {success=false,message=error});
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.InventoryItems WHERE Name=@Name AND ItemId<>@Id AND IsDeleted=0",P("@Name",r.Name.Trim()),P("@Id",id));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="An inventory item with this name already exists."});
        var n=await Db.ExecuteAsync("UPDATE dbo.InventoryItems SET Name=@Name,Unit=@Unit,ReorderLevel=@Reorder,CostPrice=@Cost,IsActive=@Active WHERE ItemId=@Id AND IsDeleted=0",
            P("@Id",id),P("@Name",r.Name.Trim()),P("@Unit",r.Unit.Trim()),P("@Reorder",r.ReorderLevel),P("@Cost",r.CostPrice),P("@Active",r.IsActive));
        return n==0?NotFound():Success(null,"Inventory item updated.");
    }

    [HttpDelete("items/{id:guid}"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var n=await Db.ExecuteAsync("UPDATE dbo.InventoryItems SET IsDeleted=1,IsActive=0 WHERE ItemId=@Id AND IsDeleted=0",P("@Id",id));
        return n==0?NotFound():Success(null,"Inventory item archived.");
    }

    [HttpPost("movements"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> Movement(StockMovementRequest r)
    {
        var type=r.MovementType?.Trim().ToUpperInvariant();
        if(r.Quantity<=0||r.Quantity>1000000000m||(type!="IN"&&type!="OUT")||r.Reference?.Length>100||r.Notes?.Length>500)
            return BadRequest(new {success=false,message="Movement must be IN or OUT with a positive quantity; reference and notes are limited to 100 and 500 characters."});
        var change=type=="IN"?r.Quantity:-r.Quantity;
        var n=await Db.ExecuteAsync("UPDATE dbo.InventoryItems SET CurrentStock=CurrentStock+@Change WHERE ItemId=@Id AND IsDeleted=0 AND IsActive=1 AND CurrentStock+@Change>=0; IF @@ROWCOUNT=1 INSERT dbo.StockMovements(MovementId,ItemId,MovementType,Quantity,Reference,Notes,CreatedBy,CreatedAt) VALUES(NEWID(),@Id,@Type,@Qty,@Reference,@Notes,@User,SYSUTCDATETIME())",
            P("@Change",change),P("@Id",r.ItemId),P("@Type",type),P("@Qty",r.Quantity),P("@Reference",r.Reference?.Trim()),P("@Notes",r.Notes?.Trim()),P("@User",CurrentUserId));
        return n==0?BadRequest(new {success=false,message="Item not found, inactive, or insufficient stock."}):Success(null,"Stock updated.");
    }

    [HttpGet("movements")]
    public async Task<IActionResult> Movements([FromQuery]int pageNumber=1,[FromQuery]int pageSize=25,[FromQuery]Guid? itemId=null)
    {
        var page=Math.Max(1,pageNumber); var size=Math.Clamp(pageSize,1,100);
        var rows=await Db.QueryProcedureAsync("dbo.usp_StockMovements_ListPaged",P("@PageNumber",page),P("@PageSize",size+1),P("@ItemId",itemId));
        var hasMore=rows.Count>size; if(hasMore) rows.RemoveAt(size);
        return Success(new {items=rows,pageNumber=page,pageSize=size,hasMore});
    }

    private static string? Validate(string? name,string? unit,decimal stock,decimal reorder,decimal cost)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>160) return "Item name is required and must be 160 characters or fewer.";
        if(string.IsNullOrWhiteSpace(unit)||unit.Trim().Length>30) return "Unit is required and must be 30 characters or fewer.";
        if(stock<0||reorder<0||cost<0) return "Stock, reorder level and cost price cannot be negative.";
        return null;
    }
}
