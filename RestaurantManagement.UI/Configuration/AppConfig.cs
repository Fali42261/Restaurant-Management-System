namespace RestaurantManagement.UI.Configuration;

/// <summary>
/// UI-side application constants and future integration naming references.
/// DevUI is currently UI/demo-only: no API URL, gateway provider, or secret
/// configuration belongs in this client project.
/// </summary>
public static class AppConfig
{
    public static class Application
    {
        public const string Name = "swapDigit";
        public const string DefaultRole = "Staff";
        public const int RequestTimeoutSeconds = 30;
    }

    /// <summary>
    /// Route names reserved for the later backend phase. They are not active
    /// network integrations in the current UI-only build.
    /// </summary>
    public static class Routes
    {
        public const string SignIn = "api/v1/auth/sign-in";
        public const string SignOut = "api/v1/auth/sign-out";
        public const string Register = "api/v1/auth/register";
        public const string ChangePassword = "api/v1/auth/change-password";
        public const string ResetPassword = "api/v1/auth/reset-password";
        public const string Products = "api/v1/products";
        public const string Subscription = "api/v1/subscriptions";
        public const string CreatePayment = "api/v1/payments";
        public const string PaymentWebhook = "api/v1/payments/webhook";
    }

    /// <summary>
    /// Future backend naming references only. No backend controllers are
    /// implemented or called by this UI-only project at this stage.
    /// </summary>
    public static class Backend
    {
        public static class Controllers
        {
            public const string Auth = "AuthController";
            public const string Products = "ProductsController";
            public const string Orders = "OrdersController";
            public const string Tables = "TablesController";
            public const string Reservations = "ReservationsController";
            public const string Subscriptions = "SubscriptionsController";
            public const string Payments = "PaymentsController";
        }

        public static class Methods
        {
            public const string SignIn = "SignIn";
            public const string SignOut = "SignOut";
            public const string Register = "Register";
            public const string ChangePassword = "ChangePassword";
            public const string ResetPassword = "ResetPassword";
            public const string GetProducts = "GetAll";
            public const string CreateOrder = "Create";
            public const string GetTables = "GetAll";
            public const string GetReservations = "GetAll";
            public const string GetSubscription = "GetCurrent";
            public const string CreatePayment = "CreatePayment";
            public const string ConfirmPayment = "ConfirmPayment";
        }
    }
}
