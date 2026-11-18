using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.DataAccess;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Services;

namespace RestaurantManagement.API.BusinessLogic;

public sealed class AuthBusinessLogic(
    AuthDataAccess data, IPasswordHasher<string> hasher, IOptions<AppSettings> options,
    EmailService email, ILogger<AuthBusinessLogic> logger)
{
    private readonly AppSettings _settings=options.Value;
    private int MinimumPasswordLength=>Math.Max(8,_settings.Business.PasswordMinimumLength);

    public async Task<AuthResult> RegisterOwnerAsync(OwnerRegistration request)
    {
        var name=request.FullName?.Trim(); var mail=request.Email?.Trim().ToLowerInvariant(); var mobile=request.MobileNumber?.Trim();
        if(string.IsNullOrWhiteSpace(name)||name.Length>150)return Bad(400,"Full name is required and must be 150 characters or fewer.");
        if(string.IsNullOrWhiteSpace(mail)||!System.Net.Mail.MailAddress.TryCreate(mail,out _))return Bad(400,"Enter a valid email address.");
        if(!ValidMobile(mobile))return Bad(400,"Enter a valid mobile number.");
        if(!StrongPassword(request.Password))return Bad(400,PasswordMessage());
        var id=Guid.NewGuid(); var hash=hasher.HashPassword(id.ToString(),request.Password);
        var stored=await data.RegisterOwnerAsync(id,name,mail,mobile!,hash);
        if(!stored.Success)return Bad(409,"Email or mobile number is already registered.");
        logger.LogInformation("Owner registration completed. UserId={UserId}",id);
        return new(true,201,new{userId=id,role="Owner"},"Owner account created. Add your first restaurant after signing in.");
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.EmailOrMobile)||string.IsNullOrWhiteSpace(request.Password))return Bad(400,"Email/mobile and password are required.");
        var rows=await data.LoginUserAsync(request.EmailOrMobile.Trim());
        if(rows.Count==0||!Bool(rows[0],"IsActive")||hasher.VerifyHashedPassword(T(rows[0],"UserId"),T(rows[0],"PasswordHash"),request.Password)==PasswordVerificationResult.Failed)
        {logger.LogWarning("Login rejected. IdentifierProvided={IdentifierProvided}",true);return Bad(401,"Invalid login details.");}
        var user=rows[0];var id=Guid.Parse(T(user,"UserId"));var restaurants=await data.GetRestaurantsAsync(id);
        var refresh=NewToken();var now=DateTime.UtcNow;
        await data.CreateRefreshTokenAsync(id,null,Hash(refresh),now.AddDays(_settings.Jwt.RefreshTokenDays));
        var access=CreateJwt(id,T(user,"FullName"),T(user,"Email"),T(user,"Role"),null,null,now);
        logger.LogInformation("Login successful. UserId={UserId}",id);
        return Ok(new{accessToken=access,refreshToken=refresh,expiresIn=_settings.Jwt.AccessTokenMinutes*60,
            user=new{userId=id,fullName=T(user,"FullName"),email=T(user,"Email"),mobileNumber=T(user,"MobileNumber"),role=T(user,"Role")},restaurants},"Login successful.");
    }

    public async Task<AuthResult> ChangePasswordAsync(Guid userId,ChangePasswordRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.CurrentPassword)||!StrongPassword(request.NewPassword))return Bad(400,PasswordMessage());
        var rows=await data.GetPasswordRecordAsync(userId);
        if(rows.Count==0||hasher.VerifyHashedPassword(userId.ToString(),T(rows[0],"PasswordHash"),request.CurrentPassword)==PasswordVerificationResult.Failed)
        {logger.LogWarning("Change password rejected. UserId={UserId}",userId);return Bad(400,"Current password is incorrect.");}
        await data.ChangePasswordAsync(userId,hasher.HashPassword(userId.ToString(),request.NewPassword));
        logger.LogInformation("Password changed; refresh sessions revoked. UserId={UserId}",userId);
        return Ok(null,"Password changed. Please sign in again.");
    }

    public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var mail=request.Email?.Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(mail)||!System.Net.Mail.MailAddress.TryCreate(mail,out _))return Ok(null,"If the account exists, a reset code will be sent.");
        var code=RandomNumberGenerator.GetInt32(100000,1000000).ToString();
        var rows=await data.StartPasswordResetAsync(mail,Hash(code));
        if(rows.Count>0&&Bool(rows[0],"CanSend"))
        {
            try{await email.SendAsync(mail,"swapDigit password reset",$"Your password reset code is {code}. It expires in 10 minutes.");logger.LogInformation("Password reset email sent.");}
            catch(Exception ex){logger.LogError(ex,"Password reset email delivery failed.");}
        }
        logger.LogInformation("Forgot-password request processed. AccountMatched={AccountMatched}",rows.Count>0&&Bool(rows[0],"Found"));
        return Ok(null,"If the account exists, a reset code will be sent.");
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var mail=request.Email?.Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(mail)||!System.Net.Mail.MailAddress.TryCreate(mail,out _)||string.IsNullOrWhiteSpace(request.Code)||!StrongPassword(request.NewPassword))
            return Bad(400,"Reset code is invalid or expired, or the new password does not meet policy.");
        var account=await data.GetUserIdByEmailAsync(mail);\n        if(account.Count==0)return Bad(400,"Reset code is invalid or expired.");\n        var userId=Guid.Parse(T(account[0],"UserId"));\n        var rows=await data.ResetPasswordAsync(mail,Hash(request.Code.Trim()),hasher.HashPassword(userId.ToString(),request.NewPassword));
        if(rows.Count==0||!Bool(rows[0],"Success")){logger.LogWarning("Password reset rejected. Reason=InvalidExpiredOrAttemptLimit");return Bad(400,"Reset code is invalid or expired.");}
        logger.LogInformation("Password reset completed; refresh sessions revoked.");
        return Ok(null,"Password reset successful. Please sign in again.");
    }

    public async Task<AuthResult> RefreshAsync(RefreshTokenRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.RefreshToken))return Bad(401,"Refresh token is invalid or expired.");
        var refresh=NewToken();var now=DateTime.UtcNow;
        var rows=await data.RotateRefreshTokenAsync(Hash(request.RefreshToken),Hash(refresh),now.AddDays(_settings.Jwt.RefreshTokenDays));
        if(rows.Count==0||!Bool(rows[0],"Success")){logger.LogWarning("Refresh-token rotation rejected.");return Bad(401,"Refresh token is invalid, expired or already used.");}
        var row=rows[0];var id=Guid.Parse(T(row,"UserId"));
        Guid? restaurant=Guid.TryParse(N(row,"RestaurantId"),out var parsed)?parsed:null;
        var access=CreateJwt(id,T(row,"FullName"),T(row,"Email"),T(row,"Role"),restaurant,N(row,"RestaurantName"),now);
        logger.LogInformation("Refresh token rotated. UserId={UserId} RestaurantScoped={RestaurantScoped}",id,restaurant.HasValue);
        return Ok(new{accessToken=access,refreshToken=refresh,expiresIn=_settings.Jwt.AccessTokenMinutes*60,restaurantId=restaurant});
    }

    public async Task<AuthResult> MeAsync(Guid userId)
    {
        var rows=await data.GetMeAsync(userId);if(rows.Count==0)return Bad(404,"User account was not found.");
        logger.LogDebug("Current-user profile retrieved. UserId={UserId}",userId);return Ok(rows[0]);
    }

    public async Task<AuthResult> LogoutAsync(Guid userId)
    {
        await data.LogoutAsync(userId);logger.LogInformation("Logout completed; refresh sessions revoked. UserId={UserId}",userId);
        return Ok(null,"Logged out.");
    }

    private string CreateJwt(Guid id,string name,string mail,string role,Guid? restaurant,string? restaurantName,DateTime now)
    {
        var claims=new List<Claim>{new(JwtRegisteredClaimNames.Sub,id.ToString()),new(JwtRegisteredClaimNames.Email,mail),new(ClaimTypes.NameIdentifier,id.ToString()),new(ClaimTypes.Name,name),new(ClaimTypes.Role,role)};
        if(restaurant.HasValue){claims.Add(new("restaurant_id",restaurant.Value.ToString()));claims.Add(new("restaurant_name",restaurantName??""));}
        var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Jwt.SigningKey));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(_settings.Jwt.Issuer,_settings.Jwt.Audience,claims,now,now.AddMinutes(_settings.Jwt.AccessTokenMinutes),new SigningCredentials(key,SecurityAlgorithms.HmacSha256)));
    }
    private bool StrongPassword(string? p)=>!string.IsNullOrWhiteSpace(p)&&p.Length>=MinimumPasswordLength&&p.Any(char.IsUpper)&&p.Any(char.IsLower)&&p.Any(char.IsDigit)&&p.Any(c=>!char.IsLetterOrDigit(c));
    private string PasswordMessage()=>$"Password must be at least {MinimumPasswordLength} characters and include uppercase, lowercase, number and symbol.";
    private static bool ValidMobile(string? v){if(string.IsNullOrWhiteSpace(v))return false;var s=v.Trim();if(s.StartsWith('+'))s=s[1..];return s.Length is >=10 and <=15&&s.All(char.IsDigit);}
    private static string NewToken()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private static string Hash(string v)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(v)));
    private static string T(Dictionary<string,object?> r,string k)=>r.TryGetValue(k,out var v)?v?.ToString()??"": "";
    private static string? N(Dictionary<string,object?> r,string k)=>r.TryGetValue(k,out var v)?v?.ToString():null;
    private static bool Bool(Dictionary<string,object?> r,string k)=>r.TryGetValue(k,out var v)&&v is not null&&Convert.ToBoolean(v);
    private static AuthResult Bad(int s,string m)=>new(false,s,null,m);
    private static AuthResult Ok(object? d,string m="Success")=>new(true,200,d,m);
}
public sealed record AuthResult(bool Success,int StatusCode,object? Data,string Message);