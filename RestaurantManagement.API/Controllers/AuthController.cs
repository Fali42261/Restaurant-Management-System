using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantManagement.API.BusinessLogic;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    SqlDataAccess db,
    AuthBusinessLogic auth) : ApiControllerBase(db)
{
    [AllowAnonymous]
    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] OwnerRegistration request) =>
        ExecuteAsync(auth.RegisterOwnerAsync(request));

    [AllowAnonymous]
    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        ExecuteAsync(auth.LoginAsync(request));

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ExecuteAsync(auth.ForgotPasswordAsync(request));

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request) =>
        ExecuteAsync(auth.ResetPasswordAsync(request));

    [Authorize]
    [HttpPost("change-password")]
    public Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request) =>
        ExecuteAsync(auth.ChangePasswordAsync(CurrentUserId, request));

    [AllowAnonymous]
    [HttpPost("refresh")]
    public Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request) =>
        ExecuteAsync(auth.RefreshAsync(request));

    [Authorize]
    [HttpGet("me")]
    public Task<IActionResult> Me() =>
        ExecuteAsync(auth.MeAsync(CurrentUserId));

    [Authorize]
    [HttpPost("logout")]
    public Task<IActionResult> Logout() =>
        ExecuteAsync(auth.LogoutAsync(CurrentUserId));

    private async Task<IActionResult> ExecuteAsync(Task<AuthResult> operation)
    {
        var result = await operation;

        if (result.StatusCode == StatusCodes.Status201Created)
            return CreatedResult(result.Data, result.Message);

        if (!result.Success)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                message = result.Message,
                traceId = HttpContext.TraceIdentifier
            });

        return Success(result.Data, result.Message);
    }
}
