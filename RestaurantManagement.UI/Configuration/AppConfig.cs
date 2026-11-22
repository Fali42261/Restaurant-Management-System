namespace RestaurantManagement.UI.Configuration;

public static class AppConfig
{
    public static class Application
    {
        public const string Name = "swapDigit";
        public const string DefaultRole = "Staff";
        public const int RequestTimeoutSeconds = 30;
        public const int ToastDurationSeconds = 5;
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

    public static class Security
    {
        public const string AccessTokenStorageKey = "swapDigit.accessToken";
        public const string RefreshTokenStorageKey = "swapDigit.refreshToken";
    }

    public static class Routes
    {
        public const string SignIn = "api/v1/auth/login";
        public const string SignOut = "api/v1/auth/logout";
        public const string Register = "api/v1/auth/register";
        public const string ChangePassword = "api/v1/auth/change-password";
        public const string ForgotPassword = "api/v1/auth/forgot-password";
        public const string ResetPassword = "api/v1/auth/reset-password";
        public const string RefreshToken = "api/v1/auth/refresh";
        public const string CurrentUser = "api/v1/auth/me";
        public const string UserRestaurants = "api/v1/users/{0}/restaurants";
        public const string CreateRestaurant = "api/v1/restaurants";
        public const string SelectRestaurant = "api/v1/restaurants/{0}/select";
        public const string DashboardSummary = "api/v1/reports/dashboard";
        public const string DashboardSales = "api/v1/reports/sales";
        public const string DashboardTopProducts = "api/v1/reports/top-products";
        public const string Products = "api/v1/products?pageNumber=1&pageSize=100";
        public const string Orders = "api/v1/orders?pageNumber=1&pageSize=100";
        public const string Bills = "api/v1/bills?pageNumber=1&pageSize=100";
        public const string Tables = "api/v1/tables";
        public const string PosCheckout = "api/v1/pos/checkout";
        public const string CreateBill = "api/v1/bills";
        public const string KitchenTickets = "api/v1/kots?pageNumber=1&pageSize=100";
        public const string ReplaceOrderItems = "api/v1/orders/{0}/items";
        public const string DeleteOrder = "api/v1/orders/{0}";
        public const string PayBill = "api/v1/pos/bills/{0}/pay";
        public const string UpdateKotStatus = "api/v1/kots/{0}/status";
        public const string SubscriptionPlans = "api/v1/settings/subscriptions/plans";
        public const string CurrentSubscription = "api/v1/settings/subscriptions/current";
        public const string SelectSubscription = "api/v1/settings/subscriptions/request";
    }
}
