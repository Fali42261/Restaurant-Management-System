using Microsoft.AspNetCore.Diagnostics;using Microsoft.Data.SqlClient;using RestaurantManagement.API.Models;
namespace RestaurantManagement.API.Middleware;
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger):IExceptionHandler{
 public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception ex,CancellationToken ct){
  var (status,message)=ex switch{
   UnauthorizedAccessException=>(StatusCodes.Status401Unauthorized,ex.Message),
   ArgumentException=>(StatusCodes.Status400BadRequest,ex.Message),
   InvalidOperationException=>(StatusCodes.Status409Conflict,ex.Message),
   KeyNotFoundException=>(StatusCodes.Status404NotFound,ex.Message),
   SqlException=>(StatusCodes.Status503ServiceUnavailable,"Database operation failed. Check server configuration and logs."),
   _=>(StatusCodes.Status500InternalServerError,"An unexpected server error occurred.")
  };
  if(status>=500)logger.LogError(ex,"API request failed");else logger.LogWarning(ex,"API request rejected");
  context.Response.StatusCode=status;await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message),ct);return true;
 }
}