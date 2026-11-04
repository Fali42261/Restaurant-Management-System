using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Repositories;
namespace RestaurantManagement.API.Services;
public interface ITokenService{
 (string Token,DateTime Expires) Access(AuthUser user);
 (string Token,string Hash,DateTime Expires) Refresh();
 string HashToken(string token);
}
public interface IAuthService{
 Task<AuthResponse> Register(RegisterRequest r,CancellationToken ct);
 Task<AuthResponse> Login(LoginRequest r,CancellationToken ct);
 Task<AuthResponse> Renew(string token,CancellationToken ct);
 Task Logout(string token,CancellationToken ct);
 Task<UserResponse> Me(long id,CancellationToken ct);
}
public sealed class TokenService(AppSettings s):ITokenService{
 public (string Token,DateTime Expires) Access(AuthUser u){
  var exp=DateTime.UtcNow.AddMinutes(s.Jwt.AccessTokenMinutes);
  var claims=new List<Claim>{new(JwtRegisteredClaimNames.Sub,u.UserId.ToString()),new(JwtRegisteredClaimNames.Email,u.Email),new(JwtRegisteredClaimNames.Name,u.FullName),new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())};
  claims.AddRange(u.Roles.Select(x=>new Claim("role",x)));
  var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s.Jwt.SigningKey));
  var jwt=new JwtSecurityToken(s.Jwt.Issuer,s.Jwt.Audience,claims,expires:exp,signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
  return(new JwtSecurityTokenHandler().WriteToken(jwt),exp);
 }
 public (string Token,string Hash,DateTime Expires) Refresh(){
  var t=Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
  return(t,HashToken(t),DateTime.UtcNow.AddDays(s.Jwt.RefreshTokenDays));
 }
 public string HashToken(string t)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(t)));
}
public sealed class AuthService(IAuthRepository repo,IPasswordHasher<AuthUser> hasher,ITokenService tokens,AppSettings cfg):IAuthService{
 public async Task<AuthResponse> Register(RegisterRequest r,CancellationToken ct){
  var email=r.Email.Trim().ToLowerInvariant();
  if(r.Password.Length<cfg.Auth.PasswordMinimumLength)throw new ArgumentException($"Password must be at least {cfg.Auth.PasswordMinimumLength} characters.");
  if(r.FullName.Trim().Length<2)throw new ArgumentException("Full name must contain at least 2 characters.");
  var country=NormalizeDigits(r.CountryCode??"");
  var mobile=NormalizeDigits(r.MobileNumber??"");
  if(mobile.Length>0&&(country.Length is <1 or >4))throw new ArgumentException("A valid country calling code is required when a mobile number is provided.");
  if(mobile.Length>0&&mobile.StartsWith('0'))mobile=mobile.TrimStart('0');
  var e164=mobile.Length==0?null:$"+{country}{mobile}";
  if(await repo.FindByEmailAsync(email,ct)!=null)throw new DuplicateAccountException("Email is already registered.","AUTH_EMAIL_ALREADY_EXISTS");
  if(e164!=null&&await repo.FindByEmailOrMobileAsync(e164,ct)!=null)throw new DuplicateAccountException("Mobile number is already registered.","AUTH_MOBILE_ALREADY_EXISTS");
  var u=new AuthUser{FullName=r.FullName.Trim(),Email=email,CountryCode=country.Length==0?null:"+"+country,MobileNumber=e164,IsActive=true};
  u.PasswordHash=hasher.HashPassword(u,r.Password);
  var refresh=tokens.Refresh();
  u.UserId=await repo.CreateUserAsync(u,refresh.Hash,refresh.Expires,ct);
  u.Roles=await repo.GetRolesAsync(u.UserId,ct);
  return Build(u,refresh);
 }
 public async Task<AuthResponse> Login(LoginRequest r,CancellationToken ct){
  var id=r.EmailOrMobile.Trim();
  if(!id.Contains('@'))id="+"+NormalizeDigits(id);
  else id=id.ToLowerInvariant();
  var u=await repo.FindByEmailOrMobileAsync(id,ct);
  if(u==null||!u.IsActive)throw new UnauthorizedAccessException("Invalid email/mobile or password.");
  var verification=hasher.VerifyHashedPassword(u,u.PasswordHash,r.Password);
  if(verification==PasswordVerificationResult.Failed)throw new UnauthorizedAccessException("Invalid email/mobile or password.");
  if(verification==PasswordVerificationResult.SuccessRehashNeeded){
   u.PasswordHash=hasher.HashPassword(u,r.Password);
   await repo.UpdatePasswordHashAsync(u.UserId,u.PasswordHash,ct);
  }
  if(cfg.Auth.RequireEmailVerification&&!u.IsEmailVerified)throw new UnauthorizedAccessException("Account verification is required.");
  u.Roles=await repo.GetRolesAsync(u.UserId,ct);
  var refresh=tokens.Refresh();
  if(!await repo.StoreRefreshTokenAsync(u.UserId,refresh.Hash,refresh.Expires,ct))throw new UnauthorizedAccessException("Account is inactive.");
  return Build(u,refresh);
 }
 public async Task<AuthResponse> Renew(string token,CancellationToken ct){
  var refresh=tokens.Refresh();
  var id=await repo.RotateRefreshTokenAsync(tokens.HashToken(token),refresh.Hash,refresh.Expires,ct);
  if(id==null)throw new UnauthorizedAccessException("Refresh token is invalid, expired, or revoked.");
  var u=await repo.FindByIdAsync(id.Value,ct)??throw new UnauthorizedAccessException("Account unavailable.");
  u.Roles=await repo.GetRolesAsync(u.UserId,ct);return Build(u,refresh);
 }
 public Task Logout(string token,CancellationToken ct)=>repo.RevokeRefreshTokenAsync(tokens.HashToken(token),ct);
 public async Task<UserResponse> Me(long id,CancellationToken ct){
  var u=await repo.FindByIdAsync(id,ct)??throw new KeyNotFoundException("User not found.");
  u.Roles=await repo.GetRolesAsync(id,ct);return User(u);
 }
 static string NormalizeDigits(string value)=>new(value.Where(char.IsDigit).ToArray());
 AuthResponse Build(AuthUser u,(string Token,string Hash,DateTime Expires) r){
  var a=tokens.Access(u);return new("Bearer",a.Token,a.Expires,r.Token,r.Expires,User(u));
 }
 static UserResponse User(AuthUser u)=>new(u.UserId,u.FullName,u.Email,u.CountryCode,u.MobileNumber,u.Roles);
}