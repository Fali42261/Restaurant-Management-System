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

        return Success(await Db.QueryProcedureAsync(
            "dbo.usp_Dashboard_Sales",
            P("@FromUtc", from.Date),
            P("@ToUtcExclusive", to.Date.AddDays(1))));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!ValidRange(from, to))
            return BadRequest(new { success = false, message = "Date range is invalid or exceeds 24 months." });

        return Success(await Db.QueryProcedureAsync(
            "dbo.usp_Dashboard_TopProducts",
            P("@FromUtc", from.Date),
            P("@ToUtcExclusive", to.Date.AddDays(1))));
    }

    private static bool ValidRange(DateTime from, DateTime to) =>
        from != default && to != default && from.Date <= to.Date && to.Date <= from.Date.AddMonths(24);
}
