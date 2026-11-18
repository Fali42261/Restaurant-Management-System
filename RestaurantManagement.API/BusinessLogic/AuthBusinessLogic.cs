using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.DataAccess;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.BusinessLogic;

public sealed class AuthBusinessLogic(
    AuthDataAccess dataAccess,
    IPasswordHasher<string> passwordHasher,
    IOptions<AppSettings> options)
{
    private readonly int _minimumPasswordLength = Math.Max(8, options.Value.Business.PasswordMinimumLength);

    public async Task<RegisterOwnerResult> RegisterOwnerAsync(OwnerRegistration request)
    {
        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var mobile = request.MobileNumber?.Trim();
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
            return RegisterOwnerResult.Invalid("Full name is required and must be 150 characters or fewer.");

        if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _))
            return RegisterOwnerResult.Invalid("Enter a valid email address.");

        if (!IsValidMobile(mobile))
            return RegisterOwnerResult.Invalid("Enter a valid mobile number with 10 to 15 digits.");

        if (!IsStrongPassword(password))
            return RegisterOwnerResult.Invalid($"Password must contain at least {_minimumPasswordLength} characters, including uppercase, lowercase, a number and a symbol.");

        var userId = Guid.NewGuid();
        var passwordHash = passwordHasher.HashPassword(userId.ToString(), password!);
        var stored = await dataAccess.RegisterOwnerAsync(userId, fullName, email, mobile!, passwordHash);

        if (!stored.Success && string.Equals(stored.ErrorCode, "Duplicate", StringComparison.OrdinalIgnoreCase))
            return RegisterOwnerResult.Conflict("Email or mobile number is already registered.");

        if (!stored.Success)
            return RegisterOwnerResult.Invalid("Owner registration could not be completed.");

        return RegisterOwnerResult.Created(stored.UserId ?? userId);
    }

    private static bool IsValidMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith('+'))
            normalized = normalized[1..];

        return normalized.Length is >= 10 and <= 15 && normalized.All(char.IsDigit);
    }

    private bool IsStrongPassword(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= _minimumPasswordLength &&
        value.Any(char.IsUpper) &&
        value.Any(char.IsLower) &&
        value.Any(char.IsDigit) &&
        value.Any(character => !char.IsLetterOrDigit(character));
}

public sealed record RegisterOwnerResult(bool Success, bool IsConflict, Guid? UserId, string Message)
{
    public static RegisterOwnerResult Invalid(string message) => new(false, false, null, message);
    public static RegisterOwnerResult Conflict(string message) => new(false, true, null, message);
    public static RegisterOwnerResult Created(Guid userId) =>
        new(true, false, userId, "Owner account created. Add your first restaurant after signing in.");
}