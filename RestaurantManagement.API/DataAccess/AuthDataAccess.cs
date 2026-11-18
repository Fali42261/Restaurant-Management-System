using RestaurantManagement.API.Infrastructure;
using Microsoft.Data.SqlClient;

namespace RestaurantManagement.API.DataAccess;

public sealed class AuthDataAccess(SqlDataAccess sqlDataAccess, ILogger<AuthDataAccess> logger)
{
    public async Task<AuthOwnerRegistrationDbResult> RegisterOwnerAsync(Guid userId,string fullName,string email,string mobileNumber,string passwordHash)
    {
        var rows=await Query("dbo.usp_Auth_RegisterOwner",
            SqlDataAccess.P("@UserId",userId),SqlDataAccess.P("@FullName",fullName),
            SqlDataAccess.P("@Email",email),SqlDataAccess.P("@MobileNumber",mobileNumber),
            SqlDataAccess.P("@PasswordHash",passwordHash));
        if(rows.Count==0)throw new InvalidOperationException("Owner registration procedure returned no result.");
        return new AuthOwnerRegistrationDbResult(Bool(rows[0],"Success"));
    }
    public Task<List<Dictionary<string,object?>>> LoginUserAsync(string identifier)=>Query("dbo.usp_Auth_LoginUser",SqlDataAccess.P("@Identifier",identifier));
    public Task<List<Dictionary<string,object?>>> GetRestaurantsAsync(Guid userId)=>Query("dbo.usp_Auth_GetUserRestaurants",SqlDataAccess.P("@UserId",userId));
    public Task<int> CreateRefreshTokenAsync(Guid userId,Guid? restaurantId,string tokenHash,DateTime expiresAt)=>Execute("dbo.usp_Auth_CreateRefreshToken",
        SqlDataAccess.P("@UserId",userId),SqlDataAccess.P("@RestaurantId",restaurantId),SqlDataAccess.P("@TokenHash",tokenHash),SqlDataAccess.P("@ExpiresAt",expiresAt));
    public Task<List<Dictionary<string,object?>>> GetPasswordRecordAsync(Guid userId)=>Query("dbo.usp_Auth_GetPasswordRecord",SqlDataAccess.P("@UserId",userId));
    public Task<List<Dictionary<string,object?>>> ChangePasswordAsync(Guid userId,string passwordHash)=>Query("dbo.usp_Auth_ChangePassword",SqlDataAccess.P("@UserId",userId),SqlDataAccess.P("@PasswordHash",passwordHash));
    public Task<List<Dictionary<string,object?>>> GetUserIdByEmailAsync(string email)=>Query("dbo.usp_Auth_GetUserIdByEmail",SqlDataAccess.P("@Email",email));
    public Task<List<Dictionary<string,object?>>> StartPasswordResetAsync(string email,string codeHash)=>Query("dbo.usp_Auth_StartPasswordReset",SqlDataAccess.P("@Email",email),SqlDataAccess.P("@CodeHash",codeHash));
    public Task<List<Dictionary<string,object?>>> ResetPasswordAsync(string email,string codeHash,string passwordHash)=>Query("dbo.usp_Auth_ResetPassword",
        SqlDataAccess.P("@Email",email),SqlDataAccess.P("@CodeHash",codeHash),SqlDataAccess.P("@PasswordHash",passwordHash));
    public Task<List<Dictionary<string,object?>>> RotateRefreshTokenAsync(string oldHash,string newHash,DateTime expiresAt)=>Query("dbo.usp_Auth_RotateRefreshToken",
        SqlDataAccess.P("@OldTokenHash",oldHash),SqlDataAccess.P("@NewTokenHash",newHash),SqlDataAccess.P("@ExpiresAt",expiresAt));
    public Task<List<Dictionary<string,object?>>> GetMeAsync(Guid userId)=>Query("dbo.usp_Auth_GetMe",SqlDataAccess.P("@UserId",userId));
    public Task<int> LogoutAsync(Guid userId)=>Execute("dbo.usp_Auth_Logout",SqlDataAccess.P("@UserId",userId));

    private async Task<List<Dictionary<string,object?>>> Query(string procedure,params SqlParameter[] parameters)
    {
        try{return await sqlDataAccess.QueryProcedureAsync(procedure,parameters);}
        catch(Exception ex){logger.LogError(ex,"Auth database procedure failed. Procedure={Procedure}",procedure);throw;}
    }
    private async Task<int> Execute(string procedure,params SqlParameter[] parameters)
    {
        try{return await sqlDataAccess.ExecuteProcedureAsync(procedure,parameters);}
        catch(Exception ex){logger.LogError(ex,"Auth database procedure failed. Procedure={Procedure}",procedure);throw;}
    }
    private static bool Bool(Dictionary<string,object?> row,string key)=>row.TryGetValue(key,out var value)&&value is not null&&Convert.ToBoolean(value);
}
public sealed record AuthOwnerRegistrationDbResult(bool Success);