using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager,Staff"), Route("api/v1/reports")]
public sealed class ReportsController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        if (CurrentRestaurantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Select a restaurant first." });

        var rows = await Db.QueryProcedureAsync("dbo.usp_Dashboard_Summary");
        return Success(rows.FirstOrDefault());
    }

    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!ValidRange(from, to))
            return BadRequest(new { success = false, message = "Date range is invalid or exceeds 24 months." });

        return Success(await Db.QueryAsync(
            "SELECT CAST(CreatedAt AS date) AS SaleDate,COUNT(1) AS BillCount,SUM(Total) AS GrossSales,SUM(Discount) AS Discounts,SUM(Tax) AS Tax FROM dbo.Bills WHERE CreatedAt>=@From AND CreatedAt<DATEADD(day,1,@To) GROUP BY CAST(CreatedAt AS date) ORDER BY SaleDate",
            P("@From", from.Date), P("@To", to.Date)));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!ValidRange(from, to))
            return BadRequest(new { success = false, message = "Date range is invalid or exceeds 24 months." });

        return Success(await Db.QueryAsync(
            "SELECT TOP 10 p.Name,SUM(oi.Quantity) AS QuantitySold,SUM(oi.Quantity*oi.UnitPrice) AS Sales FROM dbo.OrderItems oi INNER JOIN dbo.Products p ON p.ProductId=oi.ProductId INNER JOIN dbo.Orders o ON o.OrderId=oi.OrderId WHERE o.IsDeleted=0 AND o.CreatedAt>=@From AND o.CreatedAt<DATEADD(day,1,@To) GROUP BY p.Name ORDER BY QuantitySold DESC",
            P("@From", from.Date), P("@To", to.Date)));
    }

    private static bool ValidRange(DateTime from, DateTime to) =>
        from != default && to != default && from.Date <= to.Date && to.Date <= from.Date.AddMonths(24);
}
