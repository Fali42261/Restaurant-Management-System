namespace RestaurantManagement.API.Common.Constants;

/// <summary>
/// Shared length limits used by authentication data access and validation.
/// Keep these values aligned with the corresponding SQL column definitions.
/// </summary>
public static class AuthConstants
{
    public const int EmailMaxLength = 256;
    public const int MobileMaxLength = 25;
    public const int FullNameMaxLength = 150;
    public const int PasswordHashMaxLength = 500;
    public const int TokenHashLength = 64;
}
