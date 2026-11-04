using System.ComponentModel.DataAnnotations;
namespace RestaurantManagement.API.Models;
public sealed record RegisterRequest(
 [Required,StringLength(120,MinimumLength=2)] string FullName,
 [Required,EmailAddress,StringLength(254)] string Email,
 [StringLength(8)] string? CountryCode,
 [Phone,StringLength(30)] string? MobileNumber,
 [Required,MinLength(10),StringLength(128)] string Password);
public sealed record LoginRequest([Required,StringLength(254)] string EmailOrMobile,[Required,StringLength(128)] string Password);
public sealed record RefreshTokenRequest([Required,StringLength(256)] string RefreshToken);
public sealed record LogoutRequest([Required,StringLength(256)] string RefreshToken);
public sealed record UserResponse(long UserId,string FullName,string Email,string? CountryCode,string? MobileNumber,IReadOnlyList<string> Roles);
public sealed record AuthResponse(string TokenType,string AccessToken,DateTime AccessTokenExpiresAtUtc,string RefreshToken,DateTime RefreshTokenExpiresAtUtc,UserResponse User);
public sealed record ApiResponse<T>(bool Success,string Message,T? Data,string? ErrorCode=null){
 public static ApiResponse<T> Ok(T data,string message="Success")=>new(true,message,data);
 public static ApiResponse<T> Fail(string message,string errorCode)=>new(false,message,default,errorCode);
}
public sealed class DuplicateAccountException(string message,string errorCode):Exception(message){public string ErrorCode{get;}=errorCode;}
