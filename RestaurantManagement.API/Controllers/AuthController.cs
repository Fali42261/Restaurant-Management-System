using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantManagement.API.BusinessLogic;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(SqlDataAccess db,AuthBusinessLogic auth):ApiControllerBase(db)
{
 [AllowAnonymous,HttpPost("register")][HttpPost("register-owner")]
 public Task<IActionResult> RegisterOwner(OwnerRegistration request)=>Execute(auth.RegisterOwnerAsync(request));
 [AllowAnonymous,HttpPost("login")][HttpPost("sign-in")]
 public Task<IActionResult> Login(LoginRequest request)=>Execute(auth.LoginAsync(request));
 [Authorize,HttpPost("change-password")]
 public Task<IActionResult> ChangePassword(ChangePasswordRequest request)=>Execute(auth.ChangePasswordAsync(CurrentUserId,request));
 [AllowAnonymous,HttpPost("forgot-password")]
 public Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)=>Execute(auth.ForgotPasswordAsync(request));
 [AllowAnonymous,HttpPost("reset-password")]
 public Task<IActionResult> ResetPassword(ResetPasswordRequest request)=>Execute(auth.ResetPasswordAsync(request));
 [AllowAnonymous,HttpPost("refresh")]
 public Task<IActionResult> Refresh(RefreshTokenRequest request)=>Execute(auth.RefreshAsync(request));
 [Authorize,HttpGet("me")]
 public Task<IActionResult> Me()=>Execute(auth.MeAsync(CurrentUserId));
 [Authorize,HttpPost("logout")][HttpPost("sign-out")]
 public Task<IActionResult> Logout()=>Execute(auth.LogoutAsync(CurrentUserId));

 private async Task<IActionResult> Execute(Task<AuthResult> operation)
 {
  var result=await operation;
  if(result.StatusCode==201)return CreatedResult(result.Data,result.Message);
  if(!result.Success)return StatusCode(result.StatusCode,new{success=false,message=result.Message});
  return Success(result.Data,result.Message);
 }
}