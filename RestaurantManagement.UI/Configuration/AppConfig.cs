namespace RestaurantManagement.UI.Configuration;

/// <summary>
/// Central registry for application settings, API routes, backend naming conventions,
/// and payment-provider configuration keys. Keep real server secrets out of this file.
/// </summary>
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
        // Replace with the deployed API URL when the backend is connected.
        public const string BaseUrl = "";
        public const string ApiVersion = "v1";
    }

    /// <summary>
    /// Central API route registry. Update routes here rather than scattering strings
    /// through Views and ViewModels.
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
    /// Expected backend controller and action names. These are naming references,
    /// not proof that those controllers/actions already exist in this UI repository.
    /// Keep the backend implementation aligned with these names or update this map.
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

    /// <summary>
    /// Client-safe payment configuration only. Never put secret keys, private keys,
    /// webhook signing secrets, or database credentials in a MAUI app or Git.
    /// </summary>
    public static class PaymentGateway
    {
        public const string Provider = "NotConfigured";
        public const string Environment = "Sandbox";
        public const string PublishableKey = "";

        // These are setting names for the BACKEND deployment environment,
        // not values to fill into this mobile/desktop client.
        public const string SecretKeySettingName = "PAYMENT_GATEWAY_SECRET_KEY";
        public const string WebhookSecretSettingName = "PAYMENT_GATEWAY_WEBHOOK_SECRET";
    }

    /// <summary>
    /// Names of secure deployment settings. Store actual values in backend hosting
    /// secrets or GitHub Actions Secrets, never in source control.
    /// </summary>
    public static class SecretNames
    {
        public const string ApiBaseUrl = "SWAPDIGIT_API_BASE_URL";
        public const string PaymentPublishableKey = "PAYMENT_GATEWAY_PUBLISHABLE_KEY";
        public const string PaymentSecretKey = "PAYMENT_GATEWAY_SECRET_KEY";
        public const string PaymentWebhookSecret = "PAYMENT_GATEWAY_WEBHOOK_SECRET";
    }
}
