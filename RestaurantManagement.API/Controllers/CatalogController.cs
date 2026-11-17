using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1")]
public sealed class CatalogController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories() => Success(await Db.QueryAsync("SELECT CategoryId,Name,SortOrder,IsActive FROM dbo.Categories WHERE IsDeleted=0 ORDER BY SortOrder,Name"));
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? search, [FromQuery] Guid? categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25)
    {
        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_Products_ListPaged",
            P("@PageNumber", page), P("@PageSize", size + 1), P("@CategoryId", categoryId),
            P("@Search", string.IsNullOrWhiteSpace(search) ? null : search.Trim()));
        var hasMore = rows.Count > size;
        if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPost("products")]
    [Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> CreateProduct(CreateProductRequest r)
    {
        var id = Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.Products(ProductId,CategoryId,Name,Description,Price,ImageUrl,IsAvailable,IsDeleted,CreatedAt) VALUES(@Id,(SELECT TOP 1 CategoryId FROM dbo.Categories WHERE Name=@Category AND IsDeleted=0),@Name,@Description,@Price,@Image,1,0,SYSUTCDATETIME())",
            P("@Id", id), P("@Category", r.Category), P("@Name", r.Name), P("@Description", r.Description), P("@Price", r.Price), P("@Image", r.ImageUrl));
        return CreatedResult(new { productId = id });
    }
    [HttpPut("products/{id:guid}")]
    [Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> UpdateProduct(Guid id, CreateProductRequest r)
    {
        var count = await Db.ExecuteAsync("UPDATE dbo.Products SET CategoryId=(SELECT TOP 1 CategoryId FROM dbo.Categories WHERE Name=@Category AND IsDeleted=0),Name=@Name,Description=@Description,Price=@Price,ImageUrl=@Image,UpdatedAt=SYSUTCDATETIME() WHERE ProductId=@Id AND IsDeleted=0",
            P("@Id", id), P("@Category", r.Category), P("@Name", r.Name), P("@Description", r.Description), P("@Price", r.Price), P("@Image", r.ImageUrl));
        return count == 0 ? NotFound() : Success(null, "Product updated.");
    }
    [HttpDelete("products/{id:guid}")]
    [Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> DeleteProduct(Guid id) { var n = await Db.ExecuteAsync("UPDATE dbo.Products SET IsDeleted=1 WHERE ProductId=@Id", P("@Id", id)); return n == 0 ? NotFound() : Success(null, "Product archived."); }
}