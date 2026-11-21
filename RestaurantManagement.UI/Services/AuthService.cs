using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using RestaurantManagement.UI.Configuration;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Services.Interfaces;

namespace RestaurantManagement.UI.Services;

public sealed class AuthService : IAuthService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(AppConfig.Api.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(AppConfig.Application.RequestTimeoutSeconds)
        };
    }

    public Task<string> RegisterOwnerAsync(OwnerRegistrationRequest request, CancellationToken cancellationToken = default) =>
        PostMessageAsync<OwnerRegistrationData>(AppConfig.Routes.Register, request, cancellationToken);

    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        PostMessageAsync<object>(AppConfig.Routes.ForgotPassword, request, cancellationToken);

    public Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        PostMessageAsync<object>(AppConfig.Routes.ResetPassword, request, cancellationToken);

    public Task<string> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        SendAuthorizedMessageAsync(AppConfig.Routes.ChangePassword, request, cancellationToken);

    public async Task<User> SignInAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<LoginData>(
            AppConfig.Routes.SignIn,
            new RestaurantManagement.UI.Models.LoginRequest(identifier.Trim(), password),
            cancellationToken);

        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) || result.User is null)
            throw new AuthApiException("The API returned an incomplete login response.", HttpStatusCode.BadGateway);

        await SecureStorage.Default.SetAsync("swapDigit.accessToken", result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            await SecureStorage.Default.SetAsync("swapDigit.refreshToken", result.RefreshToken);

        var user = new User
        {
            Id = result.User.UserId,
            FullName = result.User.FullName,
            Email = result.User.Email,
            MobileNumber = result.User.MobileNumber,
            Role = result.User.Role,
            IsActive = true
        };
        return user;
    }

    public async Task<List<RestaurantInfo>> GetRestaurantsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await SendAuthorizedAsync<List<RestaurantInfo>>(HttpMethod.Get, string.Format(AppConfig.Routes.UserRestaurants, userId), null, cancellationToken);
        return result ?? [];
    }

    public async Task<RestaurantInfo> CreateRestaurantAsync(CreateRestaurantInput input, CancellationToken cancellationToken = default)
    {
        var result = await SendAuthorizedAsync<RestaurantInfo>(HttpMethod.Post, AppConfig.Routes.CreateRestaurant, input, cancellationToken);
        return result ?? throw new AuthApiException("The API did not return the created restaurant.", HttpStatusCode.BadGateway);
    }

    public async Task SelectRestaurantAsync(RestaurantInfo restaurant, CancellationToken cancellationToken = default)
    {
        var result = await SendAuthorizedAsync<RestaurantSelectionData>(HttpMethod.Post, string.Format(AppConfig.Routes.SelectRestaurant, restaurant.RestaurantId), null, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            throw new AuthApiException("The API did not return a restaurant-scoped access token.", HttpStatusCode.BadGateway);
        await SecureStorage.Default.SetAsync("swapDigit.accessToken", result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            await SecureStorage.Default.SetAsync("swapDigit.refreshToken", result.RefreshToken);
        RestaurantSession.Select(restaurant);
    }

    private static async Task<T?> SendAuthorizedAsync<T>(HttpMethod method, string route, object? payload, CancellationToken cancellationToken)
    {
        var token = await SecureStorage.Default.GetAsync("swapDigit.accessToken");
        if (string.IsNullOrWhiteSpace(token)) throw new AuthApiException("Please sign in again.", HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(method, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (payload is not null) request.Content = JsonContent.Create(payload, options: JsonOptions);
        using var response = await Client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiEnvelope<T>? envelope = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions); }
            catch (JsonException) { }
        }
        if (!response.IsSuccessStatusCode || envelope?.Success != true)
            throw new AuthApiException(envelope?.Message ?? $"API request failed ({(int)response.StatusCode}).", response.StatusCode);
        return envelope.Data;
    }

    public async Task<List<DashboardSalesPoint>> GetDashboardSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var route = $"{AppConfig.Routes.DashboardSales}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await SendAuthorizedAsync<List<DashboardSalesPoint>>(HttpMethod.Get, route, null, cancellationToken) ?? [];
    }

    public async Task<List<DashboardTopProduct>> GetDashboardTopProductsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var route = $"{AppConfig.Routes.DashboardTopProducts}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await SendAuthorizedAsync<List<DashboardTopProduct>>(HttpMethod.Get, route, null, cancellationToken) ?? [];
    }

    public async Task<List<KitchenTicketApiModel>> GetKitchenTicketsAsync(CancellationToken cancellationToken = default)
    {
        var page = await SendAuthorizedAsync<PagedApiResult<KitchenTicketApiModel>>(
            HttpMethod.Get, AppConfig.Routes.KitchenTickets, null, cancellationToken);
        return page?.Items ?? [];
    }

    public async Task UpdateKitchenTicketStatusAsync(Guid ticketId, string status, CancellationToken cancellationToken = default)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("A valid kitchen ticket is required.", nameof(ticketId));
        var allowed = new[] { "New", "Preparing", "Ready", "Served", "Cancelled" };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid kitchen ticket status.", nameof(status));

        await SendAuthorizedAsync<object>(
            HttpMethod.Put, $"api/v1/kots/{ticketId}/status",
            new { Status = allowed.First(x => x.Equals(status, StringComparison.OrdinalIgnoreCase)) },
            cancellationToken);
    }

    public async Task<DashboardSummary?> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await SendAuthorizedAsync<DashboardSummary>(HttpMethod.Get, AppConfig.Routes.DashboardSummary, null, cancellationToken);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync("swapDigit.accessToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, AppConfig.Routes.SignOut);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await Client.SendAsync(request, cancellationToken);
            }
        }
        finally
        {
            SecureStorage.Default.Remove("swapDigit.accessToken");
            SecureStorage.Default.Remove("swapDigit.refreshToken");
            RestaurantSession.Clear();
        }
    }

    private static async Task<string> PostMessageAsync<T>(string route, object payload, CancellationToken cancellationToken)
    {
        using var response = await Client.PostAsJsonAsync(route, payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiEnvelope<T>? envelope = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions); }
            catch (JsonException) { }
        }

        if (!response.IsSuccessStatusCode || envelope?.Success != true)
            throw new AuthApiException(envelope?.Message ?? $"API request failed ({(int)response.StatusCode}).", response.StatusCode);

        return string.IsNullOrWhiteSpace(envelope.Message) ? "Request completed successfully." : envelope.Message;
    }

    private static async Task<string> SendAuthorizedMessageAsync(string route, object payload, CancellationToken cancellationToken)
    {
        var token = await SecureStorage.Default.GetAsync("swapDigit.accessToken");
        if (string.IsNullOrWhiteSpace(token))
            throw new AuthApiException("Please sign in again.", HttpStatusCode.Unauthorized);

        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        using var response = await Client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiEnvelope<object>? envelope = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiEnvelope<object>>(body, JsonOptions); }
            catch (JsonException) { }
        }

        if (!response.IsSuccessStatusCode || envelope?.Success != true)
            throw new AuthApiException(envelope?.Message ?? $"API request failed ({(int)response.StatusCode}).", response.StatusCode);

        return string.IsNullOrWhiteSpace(envelope.Message) ? "Request completed successfully." : envelope.Message;
    }

    private static async Task<T?> PostAsync<T>(
        string route,
        object payload,
        CancellationToken cancellationToken)
    {
        using var response = await Client.PostAsJsonAsync(route, payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiEnvelope<T>? envelope = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                // A non-JSON response is reported below as a safe API error.
            }
        }

        if (!response.IsSuccessStatusCode || envelope?.Success != true)
        {
            var message = envelope?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = response.StatusCode == HttpStatusCode.Conflict
                    ? "An owner account already exists."
                    : $"API request failed ({(int)response.StatusCode}).";

            throw new AuthApiException(message, response.StatusCode);
        }

        return envelope.Data;
    }

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private sealed class OwnerRegistrationData
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }


    private sealed class RestaurantSelectionData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    private sealed class LoginData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public LoginUser? User { get; set; }
    }

    private sealed class LoginUser
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? MobileNumber { get; set; }
        public string Role { get; set; } = "Staff";
    }
}

public sealed class AuthApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
