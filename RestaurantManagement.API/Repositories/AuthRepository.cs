using System.Data;
using Microsoft.Data.SqlClient;
using RestaurantManagement.API.Models;
namespace RestaurantManagement.API.Repositories;
public sealed class AuthRepository(ISqlConnectionFactory factory,IConfiguration config):IAuthRepository{
 int Timeout=>config.GetValue<int>("AppSettings:Database:CommandTimeoutSeconds",30);
 static AuthUser Map(SqlDataReader r)=>new(){
  UserId=r.GetInt64(0),FullName=r.GetString(1),Email=r.GetString(2),
  CountryCode=r.IsDBNull(3)?null:r.GetString(3),MobileNumber=r.IsDBNull(4)?null:r.GetString(4),
  PasswordHash=r.GetString(5),IsActive=r.GetBoolean(6),IsEmailVerified=r.GetBoolean(7)};
 async Task<AuthUser?> Find(string sql,string key,object value,CancellationToken ct){
  await using var c=factory.CreateConnection();await c.OpenAsync(ct);
  await using var cmd=new SqlCommand(sql,c){CommandTimeout=Timeout};
  var p=cmd.Parameters.Add(key,value is long?SqlDbType.BigInt:SqlDbType.NVarChar);
  if(value is string)p.Size=254;p.Value=value;
  await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Map(r):null;
 }
 const string Columns="SELECT TOP(1) UserId,FullName,Email,CountryCode,MobileNumber,PasswordHash,IsActive,IsEmailVerified FROM auth.Users ";
 public Task<AuthUser?> FindByEmailOrMobileAsync(string x,CancellationToken ct)=>Find(Columns+"WHERE (Email=@v OR MobileNumber=@v) AND IsDeleted=0;","@v",x,ct);
 public Task<AuthUser?> FindByEmailAsync(string x,CancellationToken ct)=>Find(Columns+"WHERE Email=@v AND IsDeleted=0;","@v",x,ct);
 public Task<AuthUser?> FindByIdAsync(long id,CancellationToken ct)=>Find(Columns+"WHERE UserId=@v AND IsDeleted=0 AND IsActive=1;","@v",id,ct);
 public async Task<long> CreateUserAsync(AuthUser u,string hash,DateTime expiry,CancellationToken ct){
  const string sql="SET XACT_ABORT ON;BEGIN TRAN;INSERT auth.Users(FullName,Email,CountryCode,MobileNumber,PasswordHash,IsActive,IsEmailVerified,IsDeleted,CreatedAtUtc) VALUES(@n,@e,@cc,@m,@p,1,0,0,SYSUTCDATETIME());DECLARE @id BIGINT=CONVERT(BIGINT,SCOPE_IDENTITY());INSERT auth.UserRoles(UserId,RoleId) SELECT @id,RoleId FROM auth.Roles WHERE RoleName=N'Staff';INSERT auth.RefreshTokens(UserId,TokenHash,ExpiresAtUtc,CreatedAtUtc) VALUES(@id,@h,@x,SYSUTCDATETIME());COMMIT;SELECT @id;";
  await using var c=factory.CreateConnection();await c.OpenAsync(ct);await using var cmd=new SqlCommand(sql,c){CommandTimeout=Timeout};
  cmd.Parameters.Add("@n",SqlDbType.NVarChar,120).Value=u.FullName;
  cmd.Parameters.Add("@e",SqlDbType.NVarChar,254).Value=u.Email;
  cmd.Parameters.Add("@cc",SqlDbType.NVarChar,8).Value=(object?)u.CountryCode??DBNull.Value;
  cmd.Parameters.Add("@m",SqlDbType.NVarChar,30).Value=(object?)u.MobileNumber??DBNull.Value;
  cmd.Parameters.Add("@p",SqlDbType.NVarChar,500).Value=u.PasswordHash;
  cmd.Parameters.Add("@h",SqlDbType.Char,64).Value=hash;
  cmd.Parameters.Add("@x",SqlDbType.DateTime2).Value=expiry;
  try{return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));}
  catch(SqlException ex) when(ex.Number is 2601 or 2627){throw new DuplicateAccountException("Email or mobile number is already registered.","AUTH_ACCOUNT_ALREADY_EXISTS");}
 }
 public async Task<IReadOnlyList<string>> GetRolesAsync(long id,CancellationToken ct){
  var a=new List<string>();await using var c=factory.CreateConnection();await c.OpenAsync(ct);
  await using var cmd=new SqlCommand("SELECT r.RoleName FROM auth.Roles r JOIN auth.UserRoles ur ON ur.RoleId=r.RoleId WHERE ur.UserId=@id ORDER BY r.RoleName",c){CommandTimeout=Timeout};
  cmd.Parameters.Add("@id",SqlDbType.BigInt).Value=id;await using var r=await cmd.ExecuteReaderAsync(ct);
  while(await r.ReadAsync(ct))a.Add(r.GetString(0));return a;
 }
 public async Task<bool> StoreRefreshTokenAsync(long id,string hash,DateTime expiry,CancellationToken ct){
  await using var c=factory.CreateConnection();await c.OpenAsync(ct);
  await using var cmd=new SqlCommand("INSERT auth.RefreshTokens(UserId,TokenHash,ExpiresAtUtc,CreatedAtUtc) SELECT @id,@h,@x,SYSUTCDATETIME() WHERE EXISTS(SELECT 1 FROM auth.Users WHERE UserId=@id AND IsActive=1 AND IsDeleted=0);SELECT @@ROWCOUNT;",c){CommandTimeout=Timeout};
  cmd.Parameters.Add("@id",SqlDbType.BigInt).Value=id;cmd.Parameters.Add("@h",SqlDbType.Char,64).Value=hash;cmd.Parameters.Add("@x",SqlDbType.DateTime2).Value=expiry;
  return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==1;
 }
 public async Task<long?> RotateRefreshTokenAsync(string oldHash,string newHash,DateTime expiry,CancellationToken ct){
  const string sql="SET XACT_ABORT ON;BEGIN TRAN;DECLARE @id BIGINT=NULL;SELECT @id=t.UserId FROM auth.RefreshTokens t WITH(UPDLOCK,ROWLOCK) JOIN auth.Users u ON u.UserId=t.UserId AND u.IsActive=1 AND u.IsDeleted=0 WHERE t.TokenHash=@old AND t.RevokedAtUtc IS NULL AND t.ExpiresAtUtc>SYSUTCDATETIME();IF @id IS NOT NULL BEGIN UPDATE auth.RefreshTokens SET RevokedAtUtc=SYSUTCDATETIME(),ReplacedByTokenHash=@new WHERE TokenHash=@old;INSERT auth.RefreshTokens(UserId,TokenHash,ExpiresAtUtc,CreatedAtUtc) VALUES(@id,@new,@exp,SYSUTCDATETIME());END;COMMIT;SELECT @id;";
  await using var c=factory.CreateConnection();await c.OpenAsync(ct);await using var cmd=new SqlCommand(sql,c){CommandTimeout=Timeout};
  cmd.Parameters.Add("@old",SqlDbType.Char,64).Value=oldHash;cmd.Parameters.Add("@new",SqlDbType.Char,64).Value=newHash;cmd.Parameters.Add("@exp",SqlDbType.DateTime2).Value=expiry;
  var x=await cmd.ExecuteScalarAsync(ct);return x is null or DBNull?null:Convert.ToInt64(x);
 }
 public async Task UpdatePasswordHashAsync(long userId,string passwordHash,CancellationToken ct){
  await using var c=factory.CreateConnection();await c.OpenAsync(ct);
  await using var cmd=new SqlCommand("UPDATE auth.Users SET PasswordHash=@p,UpdatedAtUtc=SYSUTCDATETIME() WHERE UserId=@id AND IsActive=1 AND IsDeleted=0",c){CommandTimeout=Timeout};
  cmd.Parameters.Add("@p",SqlDbType.NVarChar,500).Value=passwordHash;cmd.Parameters.Add("@id",SqlDbType.BigInt).Value=userId;
  await cmd.ExecuteNonQueryAsync(ct);
 }
 public async Task RevokeRefreshTokenAsync(string hash,CancellationToken ct){
  await using var c=factory.CreateConnection();await c.OpenAsync(ct);
  await using var cmd=new SqlCommand("UPDATE auth.RefreshTokens SET RevokedAtUtc=SYSUTCDATETIME() WHERE TokenHash=@h AND RevokedAtUtc IS NULL",c){CommandTimeout=Timeout};
  cmd.Parameters.Add("@h",SqlDbType.Char,64).Value=hash;await cmd.ExecuteNonQueryAsync(ct);
 }
}