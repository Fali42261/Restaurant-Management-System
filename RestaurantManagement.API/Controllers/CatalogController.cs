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
    public async Task<IActionResult> Products([FromQuery] string? search, [FromQuery] Guid? categoryId) =>
        Success(await Db.QueryAsync("SELECT p.ProductId,p.Name,p.Description,p.Price,p.ImageUrl,p.IsAvailable,p.CategoryId,c.Name AS CategoryName FROM dbo.Products p LEFT JOIN dbo.Categories c ON c.CategoryId=p.CategoryId WHERE p.IsDeleted=0 AND (@Search IS NULL OR p.Name LIKE '%'+@Search+'%') AND (@CategoryId IS NULL OR p.CategoryId=@CategoryId) ORDER BY c.Name,p.Name", P("@Search", search), P("@CategoryId", categoryId)));
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