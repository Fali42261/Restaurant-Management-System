using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager"), Route("api/v1/reports")]
public sealed class ReportsController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        var rows=await Db.QueryAsync("SELECT (SELECT COUNT(1) FROM dbo.Orders WHERE CAST(CreatedAt AS date)=CAST(SYSUTCDATETIME() AS date) AND IsDeleted=0) AS TodayOrders,(SELECT ISNULL(SUM(Total),0) FROM dbo.Bills WHERE CAST(CreatedAt AS date)=CAST(SYSUTCDATETIME() AS date)) AS TodaySales,(SELECT COUNT(1) FROM dbo.Reservations WHERE ReservationDate=CAST(SYSUTCDATETIME() AS date) AND IsDeleted=0) AS TodayReservations,(SELECT COUNT(1) FROM dbo.RestaurantTables WHERE Status='Available' AND IsDeleted=0) AS AvailableTables,(SELECT COUNT(1) FROM dbo.Orders WHERE Status NOT IN('Completed','Cancelled') AND IsDeleted=0) AS ActiveOrders");
        return Success(rows.FirstOrDefault());
    }

    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery]DateTime from,[FromQuery]DateTime to)
    {
        if(!ValidRange(from,to)) return BadRequest(new {success=false,message="Date range is invalid or exceeds 24 months."});
        return Success(await Db.QueryAsync("SELECT CAST(CreatedAt AS date) AS SaleDate,COUNT(1) AS BillCount,SUM(Total) AS GrossSales,SUM(Discount) AS Discounts,SUM(Tax) AS Tax FROM dbo.Bills WHERE CreatedAt>=@From AND CreatedAt<DATEADD(day,1,@To) GROUP BY CAST(CreatedAt AS date) ORDER BY SaleDate",P("@From",from.Date),P("@To",to.Date)));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery]DateTime from,[FromQuery]DateTime to)
    {
        if(!ValidRange(from,to)) return BadRequest(new {success=false,message="Date range is invalid or exceeds 24 months."});
        return Success(await Db.QueryAsync("SELECT TOP 10 p.Name,SUM(oi.Quantity) AS QuantitySold,SUM(oi.Quantity*oi.UnitPrice) AS Sales FROM dbo.OrderItems oi INNER JOIN dbo.Products p ON p.ProductId=oi.ProductId INNER JOIN dbo.Orders o ON o.OrderId=oi.OrderId WHERE o.CreatedAt>=@From AND o.CreatedAt<DATEADD(day,1,@To) GROUP BY p.Name ORDER BY QuantitySold DESC",P("@From",from.Date),P("@To",to.Date)));
    }

    private static bool ValidRange(DateTime from,DateTime to)=>from!=default&&to!=default&&from.Date<=to.Date&&to.Date<=from.Date.AddMonths(24);
}
