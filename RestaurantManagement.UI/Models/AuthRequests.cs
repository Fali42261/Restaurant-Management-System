namespace RestaurantManagement.UI.Models;

public sealed record LoginRequest(string EmailOrMobile, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
