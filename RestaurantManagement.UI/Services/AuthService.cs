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

    public Task<string> RegisterOwnerAsync(OwnerRegistrationRequest request, CancellationToken cancellationToken = default) =>
        Api.PostMessageAsync(AppConfig.Routes.Register, request, cancellationToken: cancellationToken);

    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        Api.PostMessageAsync(AppConfig.Routes.ForgotPassword, request, cancellationToken: cancellationToken);

    public Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        Api.PostMessageAsync(AppConfig.Routes.ResetPassword, request, cancellationToken: cancellationToken);

    public Task<string> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        Api.PostMessageAsync(AppConfig.Routes.ChangePassword, request, authorized: true, cancellationToken);

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
        var result = await Api.GetAsync<List<RestaurantInfo>>(string.Format(AppConfig.Routes.UserRestaurants, userId), authorized: true, cancellationToken);
        return result ?? [];
    }

    public async Task<RestaurantInfo> CreateRestaurantAsync(CreateRestaurantInput input, CancellationToken cancellationToken = default)
    {
        var result = await Api.PostAsync<RestaurantInfo>(AppConfig.Routes.CreateRestaurant, input, authorized: true, cancellationToken);
        return result ?? throw new AuthApiException("The API did not return the created restaurant.", HttpStatusCode.BadGateway);
    }

    public async Task SelectRestaurantAsync(RestaurantInfo restaurant, CancellationToken cancellationToken = default)
    {
        var result = await Api.PostAsync<RestaurantSelectionResponse>(string.Format(AppConfig.Routes.SelectRestaurant, restaurant.RestaurantId), authorized: true, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            throw new AuthApiException("The API did not return a restaurant-scoped access token.", HttpStatusCode.BadGateway);
        await SecureStorage.Default.SetAsync("swapDigit.accessToken", result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            await SecureStorage.Default.SetAsync("swapDigit.refreshToken", result.RefreshToken);
        RestaurantSession.Select(restaurant);
    }

    private static Task<T?> SendAuthorizedAsync<T>(HttpMethod method, string route, object? payload, CancellationToken cancellationToken) =>
        Api.SendAsync<T>(method, route, payload, authorized: true, cancellationToken);

    private static Task<string> PostMessageAsync<T>(string route, object payload, CancellationToken cancellationToken) =>
        Api.PostMessageAsync(route, payload, cancellationToken: cancellationToken);

    private static Task<string> SendAuthorizedMessageAsync(string route, object payload, CancellationToken cancellationToken) =>
        Api.PostMessageAsync(route, payload, authorized: true, cancellationToken);

    private static Task<T?> PostAsync<T>(string route, object payload, CancellationToken cancellationToken) =>
        Api.PostAsync<T>(route, payload, cancellationToken: cancellationToken);

}

public sealed class AuthApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
