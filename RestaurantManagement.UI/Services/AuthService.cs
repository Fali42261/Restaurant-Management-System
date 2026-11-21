using System.Net;
using Microsoft.Maui.Storage;
using RestaurantManagement.UI.Configuration;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Services.Interfaces;
using RestaurantManagement.UI.Services.Api;
using RestaurantManagement.UI.Models.Responses;

namespace RestaurantManagement.UI.Services;

public sealed class AuthService : IAuthService
{
    private static readonly ApiClient Api = new();

    public async Task<string> RegisterOwnerAsync(OwnerRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Api.PostResponseAsync<OwnerRegistrationResponse>(
            AppConfig.Routes.Register, request, cancellationToken: cancellationToken);
        if (response.Data is null || response.Data.UserId == Guid.Empty ||
            !string.Equals(response.Data.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            throw new AuthApiException("The API did not return a valid owner account response.", HttpStatusCode.BadGateway);
        return string.IsNullOrWhiteSpace(response.Message) ? "Owner account created successfully." : response.Message;
    }

    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        Api.PostMessageAsync(AppConfig.Routes.ForgotPassword, request, cancellationToken: cancellationToken);

    public Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        Api.PostMessageAsync(AppConfig.Routes.ResetPassword, request, cancellationToken: cancellationToken);

    public async Task<string> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var message = await Api.PostMessageAsync(AppConfig.Routes.ChangePassword, request, authorized: true, cancellationToken: cancellationToken);
        SecureStorage.Default.Remove("swapDigit.accessToken");
        SecureStorage.Default.Remove("swapDigit.refreshToken");
        RestaurantSession.Clear();
        return message;
    }

    public async Task<User> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Api.PostAsync<LoginResponse>(AppConfig.Routes.SignIn, request with { EmailOrMobile = request.EmailOrMobile.Trim() }, cancellationToken: cancellationToken);

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
        var result = await Api.GetAsync<List<RestaurantInfo>>(string.Format(AppConfig.Routes.UserRestaurants, userId), authorized: true, cancellationToken: cancellationToken);
        return result ?? [];
    }

    public async Task<RestaurantInfo> CreateRestaurantAsync(CreateRestaurantInput input, CancellationToken cancellationToken = default)
    {
        var result = await Api.PostAsync<RestaurantInfo>(AppConfig.Routes.CreateRestaurant, input, authorized: true, cancellationToken: cancellationToken);
        return result ?? throw new AuthApiException("The API did not return the created restaurant.", HttpStatusCode.BadGateway);
    }

    public async Task SelectRestaurantAsync(RestaurantInfo restaurant, CancellationToken cancellationToken = default)
    {
        var result = await Api.PostAsync<RestaurantSelectionResponse>(string.Format(AppConfig.Routes.SelectRestaurant, restaurant.RestaurantId), authorized: true, cancellationToken: cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            throw new AuthApiException("The API did not return a restaurant-scoped access token.", HttpStatusCode.BadGateway);
        await SecureStorage.Default.SetAsync("swapDigit.accessToken", result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            await SecureStorage.Default.SetAsync("swapDigit.refreshToken", result.RefreshToken);
        RestaurantSession.Select(restaurant);
    }

    public async Task<List<DashboardSalesPoint>> GetDashboardSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var route = $"{AppConfig.Routes.DashboardSales}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await Api.GetAsync<List<DashboardSalesPoint>>(route, authorized: true, cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<DashboardTopProduct>> GetDashboardTopProductsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var route = $"{AppConfig.Routes.DashboardTopProducts}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await Api.GetAsync<List<DashboardTopProduct>>(route, authorized: true, cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<KitchenTicketApiModel>> GetKitchenTicketsAsync(CancellationToken cancellationToken = default)
    {
        var page = await Api.GetAsync<PagedApiResult<KitchenTicketApiModel>>(AppConfig.Routes.KitchenTickets, authorized: true, cancellationToken: cancellationToken);
        return page?.Items ?? [];
    }

    public async Task UpdateKitchenTicketStatusAsync(Guid ticketId, string status, CancellationToken cancellationToken = default)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("A valid kitchen ticket is required.", nameof(ticketId));
        var allowed = new[] { "New", "Preparing", "Ready", "Served", "Cancelled" };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid kitchen ticket status.", nameof(status));
        await Api.PutAsync<object>(string.Format(AppConfig.Routes.UpdateKotStatus, ticketId),
            new { Status = allowed.First(x => x.Equals(status, StringComparison.OrdinalIgnoreCase)) },
            authorized: true, cancellationToken: cancellationToken);
    }

    public Task<DashboardSummary?> GetDashboardSummaryAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<DashboardSummary>(AppConfig.Routes.DashboardSummary, authorized: true, cancellationToken: cancellationToken);

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try { await Api.PostAsync<object>(AppConfig.Routes.SignOut, authorized: true, cancellationToken: cancellationToken); }
        finally
        {
            SecureStorage.Default.Remove("swapDigit.accessToken");
            SecureStorage.Default.Remove("swapDigit.refreshToken");
            RestaurantSession.Clear();
        }
    }


}

public sealed class AuthApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
