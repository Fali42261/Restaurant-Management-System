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

namespace RestaurantManagement.API.Controllers;

[Authorize, Route("api/v1/restaurants")]
public sealed class RestaurantsController(SqlDataAccess db, IOptions<AppSettings> options) : ApiControllerBase(db)
{
    private readonly AppSettings _settings = options.Value;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rows = await Db.QueryAsync("SELECT r.RestaurantId,r.Name,r.IsActive,ur.Role FROM dbo.UserRestaurants ur INNER JOIN dbo.Restaurants r ON r.RestaurantId=ur.RestaurantId WHERE ur.UserId=@UserId AND ur.IsActive=1 AND r.IsActive=1 ORDER BY r.Name", P("@UserId", CurrentUserId));
        return Success(rows, "Restaurants available to this account.");
    }

    [HttpPost, Authorize(Roles="Owner")]
    public async Task<IActionResult> Create(CreateRestaurantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
            return BadRequest(new { success=false, message="Restaurant name is required and must be 160 characters or fewer." });
        var id = Guid.NewGuid();
        await Db.ExecuteAsync("INSERT dbo.Restaurants(RestaurantId,OwnerUserId,Name,IsActive,CreatedAt) VALUES(@Id,@Owner,@Name,1,SYSUTCDATETIME()); INSERT dbo.UserRestaurants(UserId,RestaurantId,Role,IsActive,CreatedAt) VALUES(@Owner,@Id,'Owner',1,SYSUTCDATETIME())", P("@Id", id), P("@Owner", CurrentUserId), P("@Name", request.Name.Trim()));
        return CreatedResult(new { restaurantId=id, name=request.Name.Trim(), subscriptionStatus="NotSubscribed" }, "Restaurant created. Each restaurant requires its own subscription.");
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
public sealed record CreateRestaurantRequest(string Name);
