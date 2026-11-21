using System.Net.Mail;
using RestaurantManagement.UI.Models;

namespace RestaurantManagement.UI.Validation;

public static class AuthRequestValidator
{
    public static string? ValidateOwner(OwnerRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) return "Enter your full name.";
        if (!IsValidEmail(request.Email)) return "Enter a valid email address.";
        var mobile = request.MobileNumber?.Trim() ?? string.Empty;
        var normalized = mobile.StartsWith("+", StringComparison.Ordinal) ? mobile[1..] : mobile;
        if (normalized.Length is < 10 or > 15 || !normalized.All(char.IsDigit))
            return "Enter a valid mobile number with 10 to 15 digits.";
        return ValidatePassword(request.Password);
    }

    public static string? ValidateLogin(string identifier, string password)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return "Enter your email or mobile number.";
        if (string.IsNullOrWhiteSpace(password)) return "Enter your password.";
        return null;
    }

    public static string? ValidateReset(string email, string code, string newPassword)
    {
        if (!IsValidEmail(email)) return "Enter a valid email address.";
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length < 4)
            return "Enter the verification code.";
        return ValidatePassword(newPassword);
    }

    public static string? ValidateChange(string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword)) return "Enter your current password.";
        return ValidatePassword(newPassword);
    }

    public static string? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(c => !char.IsLetterOrDigit(c)))
            return "Password must contain at least 8 characters with uppercase, lowercase, number and symbol.";
        return null;
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var address = new MailAddress(value.Trim());
            return address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException) { return false; }
    }
}
