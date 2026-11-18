using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.BusinessLogic;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Services;

namespace RestaurantManagement.API.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController(SqlDataAccess db, IPasswordHasher<string> hasher, IOptions<AppSettings> options, EmailService email, AuthBusinessLogic registrationBusinessLogic) : ApiControllerBase(db)
{
    private readonly AppSettings _settings = options.Value;

    [AllowAnonymous, HttpPost("register")]
    [HttpPost("register-owner")]
    public async Task<IActionResult> RegisterOwner(OwnerRegistration request)
    {
        var result = await registrationBusinessLogic.RegisterOwnerAsync(request);
        if (result.IsConflict)
            return Conflict(new { success = false, message = result.Message });
        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return CreatedResult(
            new { userId = result.UserId, role = "Owner" },
            result.Message);
    }

    [AllowAnonymous, HttpPost("login")]
    [HttpPost("sign-in")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.EmailOrMobile)||string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new {success=false,message="Email/mobile and password are required."});
        var rows = await Db.QueryAsync("SELECT TOP 1 UserId,FullName,Email,MobileNumber,PasswordHash,Role,IsActive FROM dbo.Users WHERE (Email=@Identifier OR MobileNumber=@Identifier) AND IsDeleted=0",
            P("@Identifier", request.EmailOrMobile.Trim()));
        if (rows.Count == 0 || !(bool)rows[0]["IsActive"]! || hasher.VerifyHashedPassword(rows[0]["UserId"]!.ToString()!, rows[0]["PasswordHash"]!.ToString()!, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { success = false, message = "Invalid login details." });
        var user = rows[0];
        var userId = Guid.Parse(user["UserId"]!.ToString()!);
        var restaurants = await Db.QueryAsync("SELECT r.RestaurantId,r.Name,ur.Role FROM dbo.UserRestaurants ur INNER JOIN dbo.Restaurants r ON r.RestaurantId=ur.RestaurantId WHERE ur.UserId=@UserId AND ur.IsActive=1 AND r.IsActive=1 ORDER BY r.Name", P("@UserId", userId));
        var now = DateTime.UtcNow;
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(JwtRegisteredClaimNames.Email, user["Email"]!.ToString()!), new Claim(ClaimTypes.Name, user["FullName"]!.ToString()!), new Claim(ClaimTypes.Role, user["Role"]!.ToString()!) };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Jwt.SigningKey));
        var token = new JwtSecurityToken(_settings.Jwt.Issuer, _settings.Jwt.Audience, claims, now, now.AddMinutes(_settings.Jwt.AccessTokenMinutes), new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refresh)));
        await Db.ExecuteAsync("INSERT dbo.RefreshTokens(TokenId,UserId,TokenHash,ExpiresAt,CreatedAt) VALUES(NEWID(),@UserId,@Hash,@Expires,SYSUTCDATETIME())",
            P("@UserId", userId), P("@Hash", refreshHash), P("@Expires", now.AddDays(_settings.Jwt.RefreshTokenDays)));
        return Success(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), refreshToken = refresh, expiresIn = _settings.Jwt.AccessTokenMinutes * 60, user = new { userId, fullName = user["FullName"], email = user["Email"], mobileNumber = user["MobileNumber"], role = user["Role"] }, restaurants }, "Login successful.");
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.CurrentPassword)||!IsStrongPassword(request.NewPassword))
            return BadRequest(new {success=false,message="Current password is required and the new password must meet the password policy."});
        var rows = await Db.QueryAsync("SELECT PasswordHash FROM dbo.Users WHERE UserId=@Id AND IsDeleted=0", P("@Id", CurrentUserId));
        if (rows.Count == 0 || hasher.VerifyHashedPassword(CurrentUserId.ToString(), rows[0]["PasswordHash"]!.ToString()!, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { success = false, message = "Current password is incorrect." });
        if (!IsStrongPassword(request.NewPassword)) return BadRequest(new { success = false, message = "Password must be 8+ characters with upper/lowercase, number and symbol." });
        var hash = hasher.HashPassword(CurrentUserId.ToString(), request.NewPassword);
        await Db.ExecuteAsync("UPDATE dbo.Users SET PasswordHash=@Hash,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id; UPDATE dbo.RefreshTokens SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@Id AND RevokedAt IS NULL", P("@Hash", hash), P("@Id", CurrentUserId));
        return Success(null, "Password changed.");
    }

    [AllowAnonymous, HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var emailAddress=request.Email?.Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(emailAddress)||!System.Net.Mail.MailAddress.TryCreate(emailAddress,out _))
            return Success(null,"If the account exists, a reset code will be sent.");
        var rows = await Db.QueryAsync("SELECT UserId FROM dbo.Users WHERE Email=@Email AND IsActive=1 AND IsDeleted=0", P("@Email", emailAddress));
        if (rows.Count == 0) return Success(null, "If the account exists, a reset code will be sent.");
        var userId = rows[0]["UserId"]!;
        var recent=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.PasswordResetCodes WHERE UserId=@Id AND CreatedAt>DATEADD(MINUTE,-1,SYSUTCDATETIME())",P("@Id",userId));
        if(Convert.ToInt32(recent)>0) return Success(null,"If the account exists, a reset code will be sent.");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        await Db.ExecuteAsync("DELETE dbo.PasswordResetCodes WHERE UserId=@Id; INSERT dbo.PasswordResetCodes(UserId,CodeHash,ExpiresAt,CreatedAt) VALUES(@Id,@Hash,DATEADD(MINUTE,10,SYSUTCDATETIME()),SYSUTCDATETIME())", P("@Id", userId), P("@Hash", hash));
        await email.SendAsync(emailAddress, "swapDigit password reset", $"Your password reset code is {code}. It expires in 10 minutes.");
        return Success(null, "If the account exists, a reset code will be sent.");
    }

    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var emailAddress=request.Email?.Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(emailAddress)||!System.Net.Mail.MailAddress.TryCreate(emailAddress,out _)||string.IsNullOrWhiteSpace(request.Code)||!IsStrongPassword(request.NewPassword))
            return BadRequest(new {success=false,message="Reset code is invalid or expired, or the new password does not meet policy."});
        var users=await Db.QueryAsync("SELECT TOP(1) UserId FROM dbo.Users WHERE Email=@Email AND IsActive=1 AND IsDeleted=0",P("@Email",emailAddress));
        if(users.Count==0) return BadRequest(new {success=false,message="Reset code is invalid or expired."});
        var id=Guid.Parse(users[0]["UserId"]!.ToString()!);
        var codeHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Code.Trim())));
        var passwordHash=hasher.HashPassword(id.ToString(),request.NewPassword);
        var result=await Db.QueryAsync(@"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @Consumed TABLE(UserId UNIQUEIDENTIFIER);
DELETE dbo.PasswordResetCodes
 OUTPUT deleted.UserId INTO @Consumed(UserId)
 WHERE UserId=@Id AND CodeHash=@CodeHash AND ExpiresAt>SYSUTCDATETIME() AND AttemptCount<5;
IF NOT EXISTS(SELECT 1 FROM @Consumed)
BEGIN
 ROLLBACK TRANSACTION;
 SELECT CAST(0 AS bit) AS Success;
 RETURN;
END;
UPDATE dbo.Users SET PasswordHash=@PasswordHash,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id AND IsActive=1 AND IsDeleted=0;
IF @@ROWCOUNT<>1
BEGIN
 ROLLBACK TRANSACTION;
 SELECT CAST(0 AS bit) AS Success;
 RETURN;
END;
UPDATE dbo.RefreshTokens SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@Id AND RevokedAt IS NULL;
COMMIT TRANSACTION;
SELECT CAST(1 AS bit) AS Success;",
            P("@Id",id),P("@CodeHash",codeHash),P("@PasswordHash",passwordHash));
        if(result.Count==0||!(bool)result[0]["Success"]!)
        {
            await Db.ExecuteAsync("UPDATE dbo.PasswordResetCodes SET AttemptCount=AttemptCount+1 WHERE UserId=@Id AND ExpiresAt>SYSUTCDATETIME(); DELETE dbo.PasswordResetCodes WHERE UserId=@Id AND AttemptCount>=5",P("@Id",id));
            return BadRequest(new {success=false,message="Reset code is invalid or expired."});
        }
        return Success(null,"Password reset successful. Please sign in again.");
    }

    [AllowAnonymous, HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)));
        var rows = await Db.QueryAsync("SELECT TOP 1 TokenId,UserId,RestaurantId FROM dbo.RefreshTokens WHERE TokenHash=@Hash AND RevokedAt IS NULL AND ExpiresAt>SYSUTCDATETIME()", P("@Hash", hash));
        if (rows.Count == 0) return Unauthorized(new { success = false, message = "Refresh token is invalid or expired." });
        var tokenId = Guid.Parse(rows[0]["TokenId"]!.ToString()!);
        var userId = Guid.Parse(rows[0]["UserId"]!.ToString()!);
        Guid? restaurantId = rows[0]["RestaurantId"] is null ? null : Guid.Parse(rows[0]["RestaurantId"]!.ToString()!);
        var revoked = await Db.ExecuteAsync("UPDATE dbo.RefreshTokens SET RevokedAt=SYSUTCDATETIME() WHERE TokenId=@Id AND RevokedAt IS NULL AND ExpiresAt>SYSUTCDATETIME()", P("@Id", tokenId));
        if (revoked == 0) return Unauthorized(new { success = false, message = "Refresh token has already been used." });
        var userRows = await Db.QueryAsync("SELECT FullName,Email,Role,IsActive FROM dbo.Users WHERE UserId=@Id AND IsDeleted=0", P("@Id", userId));
        if (userRows.Count == 0 || !(bool)userRows[0]["IsActive"]!) return Unauthorized(new { success = false, message = "Account is inactive." });
        var user = userRows[0];
        var now = DateTime.UtcNow;
        var role = user["Role"]!.ToString()!;
        string? restaurantName = null;
        if (restaurantId.HasValue)
        {
            var membership = await Db.QueryAsync("SELECT TOP(1) ur.Role,r.Name FROM dbo.UserRestaurants ur INNER JOIN dbo.Restaurants r ON r.RestaurantId=ur.RestaurantId WHERE ur.UserId=@UserId AND ur.RestaurantId=@RestaurantId AND ur.IsActive=1 AND r.IsActive=1", P("@UserId", userId), P("@RestaurantId", restaurantId.Value));
            if (membership.Count == 0) return Unauthorized(new { success = false, message = "Restaurant access has been removed." });
            role = membership[0]["Role"]!.ToString()!;
            restaurantName = membership[0]["Name"]!.ToString()!;
        }
        var claimsList = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, user["Email"]!.ToString()!),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, user["FullName"]!.ToString()!),
            new(ClaimTypes.Role, role)
        };
        if (restaurantId.HasValue)
        {
            claimsList.Add(new Claim("restaurant_id", restaurantId.Value.ToString()));
            claimsList.Add(new Claim("restaurant_name", restaurantName ?? ""));
        }
        var claims = claimsList.ToArray();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Jwt.SigningKey));
        var token = new JwtSecurityToken(_settings.Jwt.Issuer, _settings.Jwt.Audience, claims, now, now.AddMinutes(_settings.Jwt.AccessTokenMinutes), new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refresh)));
        await Db.ExecuteAsync("INSERT dbo.RefreshTokens(TokenId,UserId,RestaurantId,TokenHash,ExpiresAt,CreatedAt) VALUES(NEWID(),@UserId,@RestaurantId,@Hash,@Expires,SYSUTCDATETIME())", P("@UserId", userId), P("@RestaurantId", restaurantId), P("@Hash", refreshHash), P("@Expires", now.AddDays(_settings.Jwt.RefreshTokenDays)));
        return Success(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), refreshToken = refresh, expiresIn = 3600, restaurantId });
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var rows = await Db.QueryAsync("SELECT UserId,FullName,Email,MobileNumber,Role,IsActive FROM dbo.Users WHERE UserId=@Id AND IsDeleted=0", P("@Id", CurrentUserId));
        return rows.Count == 0 ? NotFound() : Success(rows[0]);
    }

    private static bool ValidMobile(string? value)
    {
        if(string.IsNullOrWhiteSpace(value)) return false;
        var normalized=value.Trim();
        if(normalized.StartsWith("+")) normalized=normalized[1..];
        return normalized.Length is >=10 and <=15 && normalized.All(char.IsDigit);
    }

    private bool IsStrongPassword(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= Math.Max(8, _settings.Business.PasswordMinimumLength) &&
        value.Any(char.IsUpper) && value.Any(char.IsLower) &&
        value.Any(char.IsDigit) && value.Any(c => !char.IsLetterOrDigit(c));

    [Authorize, HttpPost("logout")]
    [HttpPost("sign-out")]
    public async Task<IActionResult> Logout() { await Db.ExecuteAsync("UPDATE dbo.RefreshTokens SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@Id AND RevokedAt IS NULL", P("@Id", CurrentUserId)); return Success(null, "Logged out."); }
}