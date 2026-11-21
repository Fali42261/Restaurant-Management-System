namespace RestaurantManagement.UI.Models.Responses;

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public LoginUserResponse? User { get; set; }
}

public sealed class LoginUserResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string Role { get; set; } = "Staff";
}

public sealed class OwnerRegistrationResponse
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

public sealed class RestaurantSelectionResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
