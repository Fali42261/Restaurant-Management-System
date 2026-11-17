using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1/support/tickets")]
public sealed class SupportController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet] public async Task<IActionResult> List() => Success(await Db.QueryAsync("SELECT TicketId,Subject,Message,Priority,Status,CreatedAt,UpdatedAt FROM dbo.SupportTickets WHERE CreatedBy=@User ORDER BY CreatedAt DESC",P("@User",CurrentUserId)));
    [HttpPost] public async Task<IActionResult> Create(CreateSupportTicketRequest r) { var id=Guid.NewGuid(); await Db.ExecuteAsync("INSERT dbo.SupportTickets(TicketId,Subject,Message,Priority,Status,CreatedBy,CreatedAt) VALUES(@Id,@Subject,@Message,@Priority,'Open',@User,SYSUTCDATETIME())",P("@Id",id),P("@Subject",r.Subject),P("@Message",r.Message),P("@Priority",r.Priority),P("@User",CurrentUserId)); return CreatedResult(new { ticketId=id }); }
}