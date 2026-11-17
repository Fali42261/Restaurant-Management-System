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
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Services;

namespace RestaurantManagement.API.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController(SqlDataAccess db, IPasswordHasher<string> hasher, IOptions<AppSettings> options, EmailService email) : ApiControllerBase(db)
{
    private readonly AppSettings _settings = options.Value;

    [AllowAnonymous, HttpPost("register-owner")]
    public async Task<IActionResult> RegisterOwner(OwnerRegistration request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || !request.Email.Contains('@') || request.MobileNumber.Length < 10 || request.Password.Length < _settings.Business.PasswordMinimumLength)
            return BadRequest(new { success = false, message = "Enter valid owner details and a strong password." });
        var ownerExists = Convert.ToInt32(await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Users WHERE Role='Owner' AND IsDeleted=0")) > 0;
        if (ownerExists) return Conflict(new { success = false, message = "An owner account already exists. Ask the owner to create staff accounts." });
        var id = Guid.NewGuid();
        var hash = hasher.HashPassword(id.ToString(), request.Password);
        await Db.ExecuteAsync("INSERT dbo.Users(UserId,FullName,Email,MobileNumber,PasswordHash,Role,IsActive,CreatedAt) VALUES(@Id,@Name,@Email,@Mobile,@Hash,'Owner',1,SYSUTCDATETIME())",
            P("@Id", id), P("@Name", request.FullName.Trim()), P("@Email", request.Email.Trim().ToLowerInvariant()), P("@Mobile", request.MobileNumber.Trim()), P("@Hash", hash));
        return CreatedResult(new { userId = id, role = "Owner" }, "Owner account created.");
    }

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var rows = await Db.QueryAsync("SELECT TOP 1 UserId,FullName,Email,MobileNumber,PasswordHash,Role,IsActive FROM dbo.Users WHERE (Email=@Identifier OR MobileNumber=@Identifier) AND IsDeleted=0",
            P("@Identifier", request.EmailOrMobile.Trim()));
        if (rows.Count == 0 || !(bool)rows[0]["IsActive"]! || hasher.VerifyHashedPassword(rows[0]["UserId"]!.ToString()!, rows[0]["PasswordHash"]!.ToString()!, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { success = false, message = "Invalid login details." });
        var user = rows[0];
        var userId = Guid.Parse(user["UserId"]!.ToString()!);
        var now = DateTime.UtcNow;
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(JwtRegisteredClaimNames.Email, user["Email"]!.ToString()!), new Claim(ClaimTypes.Name, user["FullName"]!.ToString()!), new Claim(ClaimTypes.Role, user["Role"]!.ToString()!) };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Jwt.SigningKey));
        var token = new JwtSecurityToken(_settings.Jwt.Issuer, _settings.Jwt.Audience, claims, now, now.AddMinutes(_settings.Jwt.AccessTokenMinutes), new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refresh)));
        await Db.ExecuteAsync("INSERT dbo.RefreshTokens(TokenId,UserId,TokenHash,ExpiresAt,CreatedAt) VALUES(NEWID(),@UserId,@Hash,@Expires,SYSUTCDATETIME())",
            P("@UserId", userId), P("@Hash", refreshHash), P("@Expires", now.AddDays(_settings.Jwt.RefreshTokenDays)));
        return Success(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), refreshToken = refresh, expiresIn = _settings.Jwt.AccessTokenMinutes * 60, user = new { userId, fullName = user["FullName"], email = user["Email"], mobileNumber = user["MobileNumber"], role = user["Role"] } }, "Login successful.");
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var rows = await Db.QueryAsync("SELECT PasswordHash FROM dbo.Users WHERE UserId=@Id AND IsDeleted=0", P("@Id", CurrentUserId));
        if (rows.Count == 0 || hasher.VerifyHashedPassword(CurrentUserId.ToString(), rows[0]["PasswordHash"]!.ToString()!, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { success = false, message = "Current password is incorrect." });
        if (request.NewPassword.Length < _settings.Business.PasswordMinimumLength) return BadRequest(new { success = false, message = "New password is too short." });
        var hash = hasher.HashPassword(CurrentUserId.ToString(), request.NewPassword);
        await Db.ExecuteAsync("UPDATE dbo.Users SET PasswordHash=@Hash,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id", P("@Hash", hash), P("@Id", CurrentUserId));
        return Success(null, "Password changed.");
    }

    [AllowAnonymous, HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var rows = await Db.QueryAsync("SELECT UserId FROM dbo.Users WHERE Email=@Email AND IsActive=1 AND IsDeleted=0", P("@Email", request.Email.Trim().ToLowerInvariant()));
        if (rows.Count == 0) return Success(null, "If the account exists, a reset code will be sent.");
        var userId = rows[0]["UserId"]!;
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        await Db.ExecuteAsync("DELETE dbo.PasswordResetCodes WHERE UserId=@Id; INSERT dbo.PasswordResetCodes(UserId,CodeHash,ExpiresAt,CreatedAt) VALUES(@Id,@Hash,DATEADD(MINUTE,10,SYSUTCDATETIME()),SYSUTCDATETIME())", P("@Id", userId), P("@Hash", hash));
        await email.SendAsync(request.Email, "swapDigit password reset", $"Your password reset code is {code}. It expires in 10 minutes.");
        return Success(null, "If the account exists, a reset code will be sent.");
    }

    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Code)));
        var rows = await Db.QueryAsync("SELECT TOP 1 r.UserId FROM dbo.PasswordResetCodes r INNER JOIN dbo.Users u ON u.UserId=r.UserId WHERE u.Email=@Email AND r.CodeHash=@Hash AND r.ExpiresAt>SYSUTCDATETIME()", P("@Email", request.Email.Trim().ToLowerInvariant()), P("@Hash", hash));
        if (rows.Count == 0) return BadRequest(new { success = false, message = "Reset code is invalid or expired." });
        var id = Guid.Parse(rows[0]["UserId"]!.ToString()!);
        if (request.NewPassword.Length < _settings.Business.PasswordMinimumLength) return BadRequest(new { success = false, message = "Password is too short." });
        await Db.ExecuteAsync("UPDATE dbo.Users SET PasswordHash=@Hash,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id; DELETE dbo.PasswordResetCodes WHERE UserId=@Id",
            P("@Hash", hasher.HashPassword(id.ToString(), request.NewPassword)), P("@Id", id));
        return Success(null, "Password reset successful.");
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var rows = await Db.QueryAsync("SELECT UserId,FullName,Email,MobileNumber,Role,IsActive FROM dbo.Users WHERE UserId=@Id AND IsDeleted=0", P("@Id", CurrentUserId));
        return rows.Count == 0 ? NotFound() : Success(rows[0]);
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout() { await Db.ExecuteAsync("UPDATE dbo.RefreshTokens SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@Id AND RevokedAt IS NULL", P("@Id", CurrentUserId)); return Success(null, "Logged out."); }
}