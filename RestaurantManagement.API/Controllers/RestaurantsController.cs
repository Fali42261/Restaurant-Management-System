using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1/restaurants")]
public sealed class RestaurantsController(SqlDataAccess db, IOptions<AppSettings> options) : ApiControllerBase(db)
{
    private readonly AppSettings _settings = options.Value;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rows = await Db.QueryProcedureAsync("dbo.usp_UserRestaurants_List", P("@UserId", CurrentUserId));
        return Success(rows, "Restaurants available to this account.");
    }

    [HttpGet("~/api/v1/users/{userId:guid}/restaurants")]
    public async Task<IActionResult> GetForUser(Guid userId)
    {
        if (userId != CurrentUserId) return Forbid();
        var rows = await Db.QueryProcedureAsync("dbo.usp_UserRestaurants_List", P("@UserId", userId));
        return Success(rows, "Restaurants loaded.");
    }

    [HttpPost, Authorize(Roles="Owner")]
    public async Task<IActionResult> Create(CreateRestaurantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 ||
            string.IsNullOrWhiteSpace(request.MobileNumber) || request.MobileNumber.Trim().Length is < 10 or > 16 ||
            request.MobileNumber.Trim().TrimStart('+').Any(c => !char.IsDigit(c)) ||
            string.IsNullOrWhiteSpace(request.Address) || request.Address.Trim().Length > 500 ||
            request.OpeningTime < TimeSpan.Zero || request.OpeningTime >= TimeSpan.FromDays(1) ||
            request.ClosingTime < TimeSpan.Zero || request.ClosingTime >= TimeSpan.FromDays(1) ||
            request.OpeningTime == request.ClosingTime ||
            request.OperatingDays is null || request.OperatingDays.Length == 0 ||
            request.OperatingDays.Any(day => !new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }.Contains(day, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { success=false, message="Enter valid restaurant name, mobile, address, opening/closing time and at least one operating day." });

        var daysJson = System.Text.Json.JsonSerializer.Serialize(request.OperatingDays.Select(day => day.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var rows = await Db.QueryProcedureAsync("dbo.usp_Restaurant_CreateForOwner",
            P("@UserId", CurrentUserId), P("@Name", request.Name.Trim()), P("@MobileNumber", request.MobileNumber.Trim()),
            P("@Address", request.Address.Trim()), P("@OpeningTime", request.OpeningTime), P("@ClosingTime", request.ClosingTime),
            P("@OperatingDays", daysJson));
        if (rows.Count == 0) return StatusCode(500, new { success=false, message="Restaurant could not be created." });
        return CreatedResult(rows[0], "Restaurant created successfully.");
    }

    [HttpPut("{restaurantId:guid}")]
    [Authorize(Roles="Owner")]
    public async Task<IActionResult> Rename(Guid restaurantId,UpdateRestaurantRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Trim().Length>160)
            return BadRequest(new {success=false,message="Restaurant name is required and must be 160 characters or fewer."});
        var n=await Db.ExecuteAsync("UPDATE dbo.Restaurants SET Name=@Name,UpdatedAt=SYSUTCDATETIME() WHERE RestaurantId=@Id AND OwnerUserId=@Owner AND IsActive=1",
            P("@Name",request.Name.Trim()),P("@Id",restaurantId),P("@Owner",CurrentUserId));
        return n==0?NotFound():Success(null,"Restaurant name updated.");
    }

    [HttpPost("{restaurantId:guid}/select")]
    public async Task<IActionResult> Select(Guid restaurantId)
    {
        var rows = await Db.QueryAsync("SELECT TOP(1) r.RestaurantId,r.Name,u.UserId,u.FullName,u.Email,ur.Role FROM dbo.UserRestaurants ur INNER JOIN dbo.Restaurants r ON r.RestaurantId=ur.RestaurantId INNER JOIN dbo.Users u ON u.UserId=ur.UserId WHERE ur.UserId=@UserId AND ur.RestaurantId=@RestaurantId AND ur.IsActive=1 AND r.IsActive=1 AND u.IsActive=1 AND u.IsDeleted=0", P("@UserId", CurrentUserId), P("@RestaurantId", restaurantId));
        if (rows.Count == 0) return NotFound(new { success=false, message="Restaurant is not assigned to this account or is inactive." });
        var row=rows[0]; var now=DateTime.UtcNow;
        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, CurrentUserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, row["Email"]!.ToString()!),
            new Claim(ClaimTypes.NameIdentifier, CurrentUserId.ToString()),
            new Claim(ClaimTypes.Name, row["FullName"]!.ToString()!),
            new Claim(ClaimTypes.Role, row["Role"]!.ToString()!),
            new Claim("restaurant_id", restaurantId.ToString()),
            new Claim("restaurant_name", row["Name"]!.ToString()!)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Jwt.SigningKey));
        var token = new JwtSecurityToken(_settings.Jwt.Issuer,_settings.Jwt.Audience,claims,now,now.AddHours(1),new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
        var refreshToken=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        await Db.ExecuteAsync("INSERT dbo.RefreshTokens(TokenId,UserId,RestaurantId,TokenHash,ExpiresAt,CreatedAt) VALUES(NEWID(),@UserId,@RestaurantId,@Hash,@Expires,SYSUTCDATETIME())",
            P("@UserId",CurrentUserId),P("@RestaurantId",restaurantId),P("@Hash",refreshHash),P("@Expires",now.AddDays(_settings.Jwt.RefreshTokenDays)));
        return Success(new { accessToken=new JwtSecurityTokenHandler().WriteToken(token), refreshToken, expiresIn=3600, restaurant=new { restaurantId, name=row["Name"], role=row["Role"] } }, "Restaurant selected. Use this token for restaurant-scoped API requests.");
    }
}
public sealed record UpdateRestaurantRequest(string Name);
