namespace RestaurantManagement.API.Common.Constants;

/// <summary>
/// Canonical server route templates. Keep route attributes and UI endpoint
/// constants aligned with these values.
/// </summary>
public static class ApiRoutes
{
    public static class Auth
    {
        public const string Base = "api/v1/auth";
        public const string Register = "register";
        public const string Login = "login";
        public const string ForgotPassword = "forgot-password";
        public const string ResetPassword = "reset-password";
        public const string ChangePassword = "change-password";
        public const string Refresh = "refresh";
        public const string Me = "me";
        public const string Logout = "logout";
    }
}
