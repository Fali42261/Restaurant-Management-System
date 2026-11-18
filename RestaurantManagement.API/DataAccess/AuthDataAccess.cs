using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.DataAccess;

public sealed class AuthDataAccess(SqlDataAccess sqlDataAccess)
{
    public async Task<AuthOwnerRegistrationDbResult> RegisterOwnerAsync(
        Guid userId, string fullName, string email, string mobileNumber, string passwordHash)
    {
        var rows = await sqlDataAccess.QueryProcedureAsync(
            "dbo.usp_Auth_RegisterOwner",
            SqlDataAccess.P("@UserId", userId),
            SqlDataAccess.P("@FullName", fullName),
            SqlDataAccess.P("@Email", email),
            SqlDataAccess.P("@MobileNumber", mobileNumber),
            SqlDataAccess.P("@PasswordHash", passwordHash));

        if (rows.Count == 0)
            throw new InvalidOperationException("The owner registration stored procedure returned no result.");

        var row = rows[0];
        var success = row.TryGetValue("Success", out var successValue) &&
                      successValue is not null && Convert.ToBoolean(successValue);
        var errorCode = row.TryGetValue("ErrorCode", out var errorValue)
            ? errorValue?.ToString() ?? string.Empty : string.Empty;
        Guid? createdUserId = row.TryGetValue("UserId", out var idValue) &&
                              idValue is not null &&
                              Guid.TryParse(idValue.ToString(), out var parsedId) ? parsedId : null;

        return new AuthOwnerRegistrationDbResult(success, errorCode, createdUserId);
    }
}

public sealed record AuthOwnerRegistrationDbResult(bool Success, string ErrorCode, Guid? UserId);
public sealed record AuthOwnerRegistrationDbResult(bool Success);
