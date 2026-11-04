using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using RestaurantManagement.API.Models;
namespace RestaurantManagement.API.Middleware;
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger):IExceptionHandler{
 public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception ex,CancellationToken ct){
  var (status,message,code)=ex switch{
   UnauthorizedAccessException=>(StatusCodes.Status401Unauthorized,ex.Message,"AUTH_INVALID_CREDENTIALS"),
   ArgumentException=>(StatusCodes.Status400BadRequest,ex.Message,"VALIDATION_FAILED"),
   DuplicateAccountException d=>(StatusCodes.Status409Conflict,d.Message,d.ErrorCode),
   InvalidOperationException=>(StatusCodes.Status409Conflict,ex.Message,"AUTH_CONFLICT"),
   KeyNotFoundException=>(StatusCodes.Status404NotFound,ex.Message,"RESOURCE_NOT_FOUND"),
   SqlException sql when sql.Number is 2601 or 2627=>(StatusCodes.Status409Conflict,"Email or mobile number is already registered.","AUTH_ACCOUNT_ALREADY_EXISTS"),
   SqlException=>(StatusCodes.Status503ServiceUnavailable,"Database operation failed. Check server configuration and logs.","DATABASE_UNAVAILABLE"),
   _=>(StatusCodes.Status500InternalServerError,"An unexpected server error occurred.","INTERNAL_ERROR")
  };
  if(status>=500)logger.LogError(ex,"API request failed with {ErrorCode}",code);
  else logger.LogWarning("API request rejected with {ErrorCode}",code);
  context.Response.StatusCode=status;
  await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message,code),ct);
  return true;
 }
}