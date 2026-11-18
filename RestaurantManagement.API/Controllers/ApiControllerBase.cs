using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.Controllers;

[ApiController]
public abstract class ApiControllerBase(SqlDataAccess db) : ControllerBase
{
    protected readonly SqlDataAccess Db = db;
    protected Guid CurrentRestaurantId => Guid.TryParse(User.FindFirst("restaurant_id")?.Value, out var restaurantId) ? restaurantId : Guid.Empty;
    protected Guid CurrentUserId => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
    protected static SqlParameter P(string name, object? value) => SqlDataAccess.P(name, value);
    protected IActionResult Success(object? data, string message = "Success") => Ok(new { success = true, message, data });
    protected IActionResult CreatedResult(object? data, string message = "Created") => StatusCode(201, new { success = true, message, data });
}