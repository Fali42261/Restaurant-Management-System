using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1/support/tickets")]
public sealed class SupportController(SqlDataAccess db) : ApiControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]int pageNumber=1,[FromQuery]int pageSize=25,[FromQuery]string? status=null)
    {
        var page=Math.Max(1,pageNumber); var size=Math.Clamp(pageSize,1,100);
        var rows=await Db.QueryProcedureAsync("dbo.usp_SupportTickets_ListPaged",P("@PageNumber",page),P("@PageSize",size+1),P("@User",CurrentUserId),P("@Status",string.IsNullOrWhiteSpace(status)?null:status.Trim()));
        var hasMore=rows.Count>size; if(hasMore) rows.RemoveAt(size);
        return Success(new {items=rows,pageNumber=page,pageSize=size,hasMore});
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var rows=await Db.QueryAsync("SELECT TicketId,Subject,Message,Priority,Status,CreatedAt,UpdatedAt FROM dbo.SupportTickets WHERE TicketId=@Id AND CreatedBy=@User",P("@Id",id),P("@User",CurrentUserId));
        return rows.Count==0?NotFound():Success(rows[0]);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSupportTicketRequest r)
    {
        if(string.IsNullOrWhiteSpace(r.Subject)||r.Subject.Trim().Length>200||string.IsNullOrWhiteSpace(r.Message)||r.Message.Length>10000)
            return BadRequest(new {success=false,message="Subject is required (max 200 characters) and message is required (max 10,000 characters)."});
        var priority=r.Priority?.Trim();
        if(priority is not ("Low" or "Normal" or "High" or "Urgent")) return BadRequest(new {success=false,message="Priority must be Low, Normal, High or Urgent."});
        var id=Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.SupportTickets(TicketId,Subject,Message,Priority,Status,CreatedBy,CreatedAt) VALUES(@Id,@Subject,@Message,@Priority,'Open',@User,SYSUTCDATETIME())",P("@Id",id),P("@Subject",r.Subject.Trim()),P("@Message",r.Message.Trim()),P("@Priority",priority),P("@User",CurrentUserId));
        return CreatedResult(new {ticketId=id});
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id,UpdateStatusRequest r)
    {
        var status=r.Status?.Trim();
        if(status is not ("Open" or "InProgress" or "Resolved" or "Closed")) return BadRequest(new {success=false,message="Invalid support ticket status."});
        var n=await Db.ExecuteAsync("UPDATE dbo.SupportTickets SET Status=@Status,UpdatedAt=SYSUTCDATETIME() WHERE TicketId=@Id AND CreatedBy=@User",P("@Status",status),P("@Id",id),P("@User",CurrentUserId));
        return n==0?NotFound():Success(null,"Support ticket status updated.");
    }
}
