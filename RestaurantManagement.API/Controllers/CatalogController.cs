using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1")]
public sealed class CatalogController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories() =>
        Success(await Db.QueryAsync("SELECT CategoryId,Name,SortOrder,IsActive FROM dbo.Categories WHERE IsDeleted=0 ORDER BY SortOrder,Name"));

    [HttpPost("categories"), Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> CreateCategory(CategoryRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Trim().Length>100||request.SortOrder<0)
            return BadRequest(new {success=false,message="Category name is required (maximum 100 characters) and sort order cannot be negative."});
        var name=request.Name.Trim();
        var exists=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Categories WHERE Name=@Name AND IsDeleted=0",P("@Name",name));
        if(Convert.ToInt32(exists)>0) return Conflict(new {success=false,message="A category with this name already exists in this restaurant."});
        var id=Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.Categories(CategoryId,Name,SortOrder,IsActive,IsDeleted) VALUES(@Id,@Name,@SortOrder,1,0)",P("@Id",id),P("@Name",name),P("@SortOrder",request.SortOrder));
        return CreatedResult(new {categoryId=id,name});
    }

    [HttpPut("categories/{id:guid}"), Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> UpdateCategory(Guid id,CategoryRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Trim().Length>100||request.SortOrder<0)
            return BadRequest(new {success=false,message="Invalid category name or sort order."});
        var name=request.Name.Trim();
        var exists=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Categories WHERE Name=@Name AND CategoryId<>@Id AND IsDeleted=0",P("@Name",name),P("@Id",id));
        if(Convert.ToInt32(exists)>0) return Conflict(new {success=false,message="A category with this name already exists in this restaurant."});
        var n=await Db.ExecuteAsync("UPDATE dbo.Categories SET Name=@Name,SortOrder=@SortOrder,IsActive=@Active WHERE CategoryId=@Id AND IsDeleted=0",P("@Name",name),P("@SortOrder",request.SortOrder),P("@Active",request.IsActive),P("@Id",id));
        return n==0?NotFound():Success(null,"Category updated.");
    }

    [HttpDelete("categories/{id:guid}"), Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var used=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Products WHERE CategoryId=@Id AND IsDeleted=0",P("@Id",id));
        if(Convert.ToInt32(used)>0) return Conflict(new {success=false,message="Move or archive products in this category before deleting it."});
        var n=await Db.ExecuteAsync("UPDATE dbo.Categories SET IsDeleted=1,IsActive=0 WHERE CategoryId=@Id AND IsDeleted=0",P("@Id",id));
        return n==0?NotFound():Success(null,"Category archived.");
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery]string? search,[FromQuery]Guid? categoryId,[FromQuery]int pageNumber=1,[FromQuery]int pageSize=25)
    {
        var page=Math.Max(1,pageNumber); var size=Math.Clamp(pageSize,1,100);
        var rows=await Db.QueryProcedureAsync("dbo.usp_Products_ListPaged",P("@PageNumber",page),P("@PageSize",size+1),P("@CategoryId",categoryId),P("@Search",string.IsNullOrWhiteSpace(search)?null:search.Trim()));
        var hasMore=rows.Count>size; if(hasMore) rows.RemoveAt(size);
        return Success(new {items=rows,pageNumber=page,pageSize=size,hasMore});
    }

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT p.ProductId,p.CategoryId,c.Name AS Category,p.Name,p.Description,p.Price,p.ImageUrl,p.IsAvailable FROM dbo.Products p LEFT JOIN dbo.Categories c ON c.CategoryId=p.CategoryId WHERE p.ProductId=@Id AND p.IsDeleted=0",P("@Id",id));
        return rows.Count==0?NotFound():Success(rows[0]);
    }

    [HttpPost("products"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> CreateProduct(CreateProductRequest request)
    {
        var validation=await ValidateProduct(request);
        if(validation.Error is not null) return BadRequest(new {success=false,message=validation.Error});
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Products WHERE Name=@Name AND IsDeleted=0",P("@Name",request.Name.Trim()));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="A product with this name already exists in this restaurant."});
        var id=Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.Products(ProductId,CategoryId,Name,Description,Price,ImageUrl,IsAvailable,IsDeleted,CreatedAt) VALUES(@Id,@CategoryId,@Name,@Description,@Price,@Image,1,0,SYSUTCDATETIME())",
            P("@Id",id),P("@CategoryId",validation.CategoryId),P("@Name",request.Name.Trim()),P("@Description",request.Description?.Trim()),P("@Price",request.Price),P("@Image",request.ImageUrl?.Trim()));
        return CreatedResult(new {productId=id});
    }

    [HttpPut("products/{id:guid}"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> UpdateProduct(Guid id,CreateProductRequest request)
    {
        var validation=await ValidateProduct(request);
        if(validation.Error is not null) return BadRequest(new {success=false,message=validation.Error});
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Products WHERE Name=@Name AND ProductId<>@Id AND IsDeleted=0",P("@Name",request.Name.Trim()),P("@Id",id));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="A product with this name already exists in this restaurant."});
        var count=await Db.ExecuteAsync("UPDATE dbo.Products SET CategoryId=@CategoryId,Name=@Name,Description=@Description,Price=@Price,ImageUrl=@Image,UpdatedAt=SYSUTCDATETIME() WHERE ProductId=@Id AND IsDeleted=0",
            P("@Id",id),P("@CategoryId",validation.CategoryId),P("@Name",request.Name.Trim()),P("@Description",request.Description?.Trim()),P("@Price",request.Price),P("@Image",request.ImageUrl?.Trim()));
        return count==0?NotFound():Success(null,"Product updated.");
    }

    [HttpDelete("products/{id:guid}"),Authorize(Roles="Owner,Manager")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var count=await Db.ExecuteAsync("UPDATE dbo.Products SET IsDeleted=1,IsAvailable=0,UpdatedAt=SYSUTCDATETIME() WHERE ProductId=@Id AND IsDeleted=0",P("@Id",id));
        return count==0?NotFound():Success(null,"Product archived.");
    }

    private async Task<(Guid? CategoryId,string? Error)> ValidateProduct(CreateProductRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>160)
            return (null,"Product name is required and must be 160 characters or fewer.");
        if(r.Price<=0||r.Price>999999999999m)
            return (null,"Product price must be greater than zero and within the supported range.");
        if(r.Description?.Length>500)
            return (null,"Product description cannot exceed 500 characters.");
        if(r.ImageUrl?.Length>1000||(!string.IsNullOrWhiteSpace(r.ImageUrl)&&(!Uri.TryCreate(r.ImageUrl,UriKind.Absolute,out var uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))))
            return (null,"Image URL must be a valid HTTP or HTTPS URL.");
        if(string.IsNullOrWhiteSpace(r.Category))
            return (null,"Select a category.");
        var rows=await Db.QueryAsync("SELECT TOP(1) CategoryId FROM dbo.Categories WHERE Name=@Name AND IsActive=1 AND IsDeleted=0",P("@Name",r.Category.Trim()));
        return rows.Count==0?(null,"Selected category does not exist or is inactive."):(Guid.Parse(rows[0]["CategoryId"]!.ToString()!),null);
    }
}
public sealed record CategoryRequest(string Name,int SortOrder=0,bool IsActive=true);
