using System.ComponentModel.DataAnnotations;
namespace RestaurantManagement.API.Models;
public sealed record RegisterRequest([Required,StringLength(120,MinimumLength=2)]string FullName,[Required,EmailAddress,StringLength(254)]string Email,[Phone,StringLength(30)]string? MobileNumber,[Required,MinLength(10),StringLength(128)]string Password);
public sealed record LoginRequest([Required,StringLength(254)]string EmailOrMobile,[Required,StringLength(128)]string Password);
public sealed record RefreshTokenRequest([Required]string RefreshToken); public sealed record LogoutRequest([Required]string RefreshToken);
public sealed record UserResponse(long UserId,string FullName,string Email,string? MobileNumber,IReadOnlyList<string> Roles);
public sealed record AuthResponse(string TokenType,string AccessToken,DateTime AccessTokenExpiresAtUtc,string RefreshToken,DateTime RefreshTokenExpiresAtUtc,UserResponse User);
public sealed record ApiResponse<T>(bool Success,string Message,T? Data){public static ApiResponse<T> Ok(T data,string message="Success")=>new(true,message,data);public static ApiResponse<T> Fail(string message)=>new(false,message,default);}