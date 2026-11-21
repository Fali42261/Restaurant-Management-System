using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantManagement.API.BusinessLogic;
using RestaurantManagement.API.Common.Constants;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[ApiController]
[Route(ApiRoutes.Auth.Base)]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    SqlDataAccess db,
    AuthBusinessLogic auth) : ApiControllerBase(db)
{
    [AllowAnonymous]
    [HttpPost(ApiRoutes.Auth.Register)]
    public Task<IActionResult> Register([FromBody] OwnerRegistration request) =>
        ExecuteAsync(auth.RegisterOwnerAsync(request));

    [AllowAnonymous]
    [HttpPost(ApiRoutes.Auth.Login)]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        ExecuteAsync(auth.LoginAsync(request));

    [AllowAnonymous]
    [HttpPost(ApiRoutes.Auth.ForgotPassword)]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ExecuteAsync(auth.ForgotPasswordAsync(request));

    [AllowAnonymous]
    [HttpPost(ApiRoutes.Auth.ResetPassword)]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request) =>
        ExecuteAsync(auth.ResetPasswordAsync(request));

    [Authorize]
    [HttpPost(ApiRoutes.Auth.ChangePassword)]
    public Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request) =>
        ExecuteAsync(auth.ChangePasswordAsync(CurrentUserId, request));

    [AllowAnonymous]
    [HttpPost(ApiRoutes.Auth.Refresh)]
    public Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request) =>
        ExecuteAsync(auth.RefreshAsync(request));

    [Authorize]
    [HttpGet(ApiRoutes.Auth.Me)]
    public Task<IActionResult> Me() =>
        ExecuteAsync(auth.MeAsync(CurrentUserId));

    [Authorize]
    [HttpPost(ApiRoutes.Auth.Logout)]
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
