using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1/support/tickets")]
public sealed class SupportController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, [FromQuery] string? status = null)
    {
        var page = Math.Max(1, pageNumber); var size = Math.Clamp(pageSize, 1, 100);
        var rows = await Db.QueryProcedureAsync("dbo.usp_SupportTickets_ListPaged", P("@PageNumber", page), P("@PageSize", size + 1), P("@User", CurrentUserId), P("@Status", status));
        var hasMore = rows.Count > size; if (hasMore) rows.RemoveAt(size);
        return Success(new { items = rows, pageNumber = page, pageSize = size, hasMore });
    }
    [HttpPost] public async Task<IActionResult> Create(CreateSupportTicketRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.SupportTickets(TicketId,Subject,Message,Priority,Status,CreatedBy,CreatedAt) VALUES(@Id,@Subject,@Message,@Priority,'Open',@User,SYSUTCDATETIME())",P("@Id",id),P("@Subject",r.Subject),P("@Message",r.Message),P("@Priority",r.Priority),P("@User",CurrentUserId)); return CreatedResult(new { ticketId=id }); }
}