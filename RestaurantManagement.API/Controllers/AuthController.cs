using System.Security.Claims;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using RestaurantManagement.API.Models;using RestaurantManagement.API.Services;
namespace RestaurantManagement.API.Controllers;
[ApiController][Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth):ControllerBase{
 [AllowAnonymous,HttpPost("register")]public async Task<IActionResult> Register(RegisterRequest r,CancellationToken ct)=>StatusCode(201,ApiResponse<AuthResponse>.Ok(await auth.Register(r,ct),"Account created."));
 [AllowAnonymous,HttpPost("login")]public async Task<IActionResult> Login(LoginRequest r,CancellationToken ct)=>Ok(ApiResponse<AuthResponse>.Ok(await auth.Login(r,ct),"Login successful."));
 [AllowAnonymous,HttpPost("refresh")]public async Task<IActionResult> Refresh(RefreshTokenRequest r,CancellationToken ct)=>Ok(ApiResponse<AuthResponse>.Ok(await auth.Renew(r.RefreshToken,ct),"Token refreshed."));
 [AllowAnonymous,HttpPost("logout")]public async Task<IActionResult> Logout(LogoutRequest r,CancellationToken ct){await auth.Logout(r.RefreshToken,ct);return Ok(ApiResponse<object>.Ok(new{},"Session revoked."));}
 [Authorize,HttpGet("me")]public async Task<IActionResult> Me(CancellationToken ct){var id=User.FindFirstValue("sub");if(!long.TryParse(id,out var uid))return Unauthorized();return Ok(ApiResponse<UserResponse>.Ok(await auth.Me(uid,ct)));}
 [Authorize(Roles="Owner,Admin"),HttpGet("owner-check")]public IActionResult OwnerCheck()=>Ok(ApiResponse<object>.Ok(new{allowed=true}));
}