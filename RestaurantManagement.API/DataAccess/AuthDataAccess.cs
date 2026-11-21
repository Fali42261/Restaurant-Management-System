using System.Data;
using Microsoft.Data.SqlClient;
using RestaurantManagement.API.Infrastructure;

namespace RestaurantManagement.API.DataAccess;

/// <summary>
/// Auth module database boundary. All database work is executed through stored procedures.
/// Input values are parameterized, cancellation is propagated, and secrets are never logged.
/// </summary>
public sealed class AuthDataAccess(
    SqlDataAccess sqlDataAccess,
    ILogger<AuthDataAccess> logger)
{
    private const int EmailMaxLength = 256;
    private const int MobileMaxLength = 25;
    private const int FullNameMaxLength = 150;
    private const int PasswordHashMaxLength = 500;
    private const int TokenHashLength = 64;

    public async Task<AuthOwnerRegistrationDbResult> RegisterOwnerAsync(
        Guid userId, string fullName, string email, string mobileNumber, string passwordHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ValidateText(fullName, FullNameMaxLength, nameof(fullName));
        ValidateText(email, EmailMaxLength, nameof(email));
        ValidateText(mobileNumber, MobileMaxLength, nameof(mobileNumber));
        ValidateText(passwordHash, PasswordHashMaxLength, nameof(passwordHash));

        var rows = await Query(
            "dbo.usp_Auth_RegisterOwner",
            cancellationToken,
            GuidParameter("@UserId", userId),
            NVarCharParameter("@FullName", fullName, FullNameMaxLength),
            NVarCharParameter("@Email", email, EmailMaxLength),
            NVarCharParameter("@MobileNumber", mobileNumber, MobileMaxLength),
            NVarCharParameter("@PasswordHash", passwordHash, PasswordHashMaxLength));

        if (rows.Count == 0)
            throw new DataException("Owner registration procedure returned no result.");

        return new AuthOwnerRegistrationDbResult(ReadBoolean(rows[0], "Success"));
    }

    public Task<List<Dictionary<string, object?>>> LoginUserAsync(
        string identifier, CancellationToken cancellationToken = default)
    {
        ValidateText(identifier, EmailMaxLength, nameof(identifier));
        return Query("dbo.usp_Auth_LoginUser", cancellationToken,
            NVarCharParameter("@Identifier", identifier, EmailMaxLength));
    }

    public Task<List<Dictionary<string, object?>>> GetRestaurantsAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Query("dbo.usp_Auth_GetUserRestaurants", cancellationToken,
            GuidParameter("@UserId", userId));

    public Task<int> CreateRefreshTokenAsync(
        Guid userId, Guid? restaurantId, string tokenHash, DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(tokenHash, nameof(tokenHash));
        return Execute("dbo.usp_Auth_CreateRefreshToken", cancellationToken,
            GuidParameter("@UserId", userId),
            NullableGuidParameter("@RestaurantId", restaurantId),
            CharParameter("@TokenHash", tokenHash, TokenHashLength),
            DateTimeParameter("@ExpiresAt", expiresAt));
    }

    public Task<List<Dictionary<string, object?>>> GetPasswordRecordAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Query("dbo.usp_Auth_GetPasswordRecord", cancellationToken,
            GuidParameter("@UserId", userId));

    public Task<List<Dictionary<string, object?>>> ChangePasswordAsync(
        Guid userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        ValidateText(passwordHash, PasswordHashMaxLength, nameof(passwordHash));
        return Query("dbo.usp_Auth_ChangePassword", cancellationToken,
            GuidParameter("@UserId", userId),
            NVarCharParameter("@PasswordHash", passwordHash, PasswordHashMaxLength));
    }

    public Task<List<Dictionary<string, object?>>> GetUserIdByEmailAsync(
        string email, CancellationToken cancellationToken = default)
    {
        ValidateText(email, EmailMaxLength, nameof(email));
        return Query("dbo.usp_Auth_GetUserIdByEmail", cancellationToken,
            NVarCharParameter("@Email", email, EmailMaxLength));
    }

    public Task<List<Dictionary<string, object?>>> StartPasswordResetAsync(
        string email, string codeHash, CancellationToken cancellationToken = default)
    {
        ValidateText(email, EmailMaxLength, nameof(email));
        ValidateHash(codeHash, nameof(codeHash));
        return Query("dbo.usp_Auth_StartPasswordReset", cancellationToken,
            NVarCharParameter("@Email", email, EmailMaxLength),
            CharParameter("@CodeHash", codeHash, TokenHashLength));
    }

    public Task<List<Dictionary<string, object?>>> ResetPasswordAsync(
        string email, string codeHash, string passwordHash,
        CancellationToken cancellationToken = default)
    {
        ValidateText(email, EmailMaxLength, nameof(email));
        ValidateHash(codeHash, nameof(codeHash));
        ValidateText(passwordHash, PasswordHashMaxLength, nameof(passwordHash));
        return Query("dbo.usp_Auth_ResetPassword", cancellationToken,
            NVarCharParameter("@Email", email, EmailMaxLength),
            CharParameter("@CodeHash", codeHash, TokenHashLength),
            NVarCharParameter("@PasswordHash", passwordHash, PasswordHashMaxLength));
    }

    public Task<List<Dictionary<string, object?>>> RotateRefreshTokenAsync(
        string oldHash, string newHash, DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(oldHash, nameof(oldHash));
        ValidateHash(newHash, nameof(newHash));
        return Query("dbo.usp_Auth_RotateRefreshToken", cancellationToken,
            CharParameter("@OldTokenHash", oldHash, TokenHashLength),
            CharParameter("@NewTokenHash", newHash, TokenHashLength),
            DateTimeParameter("@ExpiresAt", expiresAt));
    }

    public Task<List<Dictionary<string, object?>>> GetMeAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Query("dbo.usp_Auth_GetMe", cancellationToken,
            GuidParameter("@UserId", userId));

    public Task<int> LogoutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Execute("dbo.usp_Auth_Logout", cancellationToken,
            GuidParameter("@UserId", userId));

    private async Task<List<Dictionary<string, object?>>> Query(
        string procedure, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        try
        {
            return await sqlDataAccess.QueryProcedureAsync(procedure, cancellationToken, parameters);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException ex)
        {
            logger.LogError(ex,
                "Auth database query failed. Procedure={Procedure}, SqlErrorNumber={SqlErrorNumber}",
                procedure, ex.Number);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth database query failed. Procedure={Procedure}", procedure);
            throw;
        }
    }

    private async Task<int> Execute(
        string procedure, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        try
        {
            return await sqlDataAccess.ExecuteProcedureAsync(procedure, cancellationToken, parameters);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException ex)
        {
            logger.LogError(ex,
                "Auth database command failed. Procedure={Procedure}, SqlErrorNumber={SqlErrorNumber}",
                procedure, ex.Number);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth database command failed. Procedure={Procedure}", procedure);
            throw;
        }
    }

    private static bool ReadBoolean(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is not null && Convert.ToBoolean(value);

    private static void ValidateText(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
    }

    private static void ValidateHash(string value, string parameterName)
    {
        if (value is null || value.Length != TokenHashLength || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("A 64-character hexadecimal SHA-256 digest is required.", parameterName);
    }

    private static SqlParameter GuidParameter(string name, Guid value) =>
        new(name, SqlDbType.UniqueIdentifier) { Value = value };

    private static SqlParameter NullableGuidParameter(string name, Guid? value) =>
        new(name, SqlDbType.UniqueIdentifier) { Value = value.HasValue ? value.Value : DBNull.Value };

    private static SqlParameter NVarCharParameter(string name, string value, int size) =>
        new(name, SqlDbType.NVarChar, size) { Value = value };

    private static SqlParameter CharParameter(string name, string value, int size) =>
        new(name, SqlDbType.Char, size) { Value = value };

    private static SqlParameter DateTimeParameter(string name, DateTime value) =>
        new(name, SqlDbType.DateTime2) { Value = value };
}

public sealed record AuthOwnerRegistrationDbResult(bool Success);
