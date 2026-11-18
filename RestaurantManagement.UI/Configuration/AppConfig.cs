namespace RestaurantManagement.UI.Configuration;

public static class AppConfig
{
    public static class Application
    {
        public const string Name = "swapDigit";
        public const string DefaultRole = "Staff";
        public const int RequestTimeoutSeconds = 30;
    }

    public static class Api
    {
        // Android Emulator -> host machine. For a physical Android phone, replace
        // this with the development PC's LAN IPv4 address (same Wi-Fi network).
#if ANDROID
        public const string BaseUrl = "http://10.0.2.2:5080/";
#else
        // Windows, iOS Simulator and Mac Catalyst development.
        public const string BaseUrl = "http://localhost:5080/";
#endif
    }

    public static class Routes
    {
        public const string SignIn = "api/v1/auth/login";
        public const string SignOut = "api/v1/auth/logout";
        public const string Register = "api/v1/auth/register";
        public const string ChangePassword = "api/v1/auth/change-password";
        public const string ResetPassword = "api/v1/auth/reset-password";
    }
}
