using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using RestaurantManagement.UI.Configuration;
using RestaurantManagement.UI.Models.Responses;
using RestaurantManagement.UI.Services;

namespace RestaurantManagement.UI.Services.Api;

/// <summary>Shared HTTP pipeline with one-time refresh-token retry for authorized requests.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri(AppConfig.Api.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(AppConfig.Application.RequestTimeoutSeconds)
        };
    }

    public Task<T?> GetAsync<T>(string route, bool authorized = false, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, route, null, authorized, cancellationToken);

    public Task<T?> PostAsync<T>(string route, object? payload = null, bool authorized = false, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, route, payload, authorized, cancellationToken);

    public Task<T?> PutAsync<T>(string route, object? payload = null, bool authorized = false, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Put, route, payload, authorized, cancellationToken);

    public Task<T?> DeleteAsync<T>(string route, bool authorized = false, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Delete, route, null, authorized, cancellationToken);

    public async Task<string> PostMessageAsync(string route, object payload, bool authorized = false, CancellationToken cancellationToken = default)
    {
        var envelope = await PostResponseAsync<JsonElement>(route, payload, authorized, cancellationToken);
        return string.IsNullOrWhiteSpace(envelope.Message) ? "Request completed successfully." : envelope.Message;
    }

    public Task<ApiResponse<T>> PostResponseAsync<T>(string route, object payload, bool authorized = false, CancellationToken cancellationToken = default) =>
        SendEnvelopeAsync<T>(HttpMethod.Post, route, payload, authorized, cancellationToken);

    public async Task<T?> SendAsync<T>(HttpMethod method, string route, object? payload = null, bool authorized = false, CancellationToken cancellationToken = default)
    {
        var envelope = await SendEnvelopeAsync<T>(method, route, payload, authorized, cancellationToken);
        return envelope.Data;
    }

    private async Task<ApiResponse<T>> SendEnvelopeAsync<T>(
        HttpMethod method, string route, object? payload, bool authorized, CancellationToken cancellationToken)
    {
        var accessToken = authorized
            ? await SecureStorage.Default.GetAsync(AppConfig.Security.AccessTokenStorageKey)
            : null;

        if (authorized && string.IsNullOrWhiteSpace(accessToken))
            throw new ApiException("Please sign in again.", HttpStatusCode.Unauthorized);

        using var response = await SendHttpRequestAsync(method, route, payload, accessToken, cancellationToken);

        if (authorized && response.StatusCode == HttpStatusCode.Unauthorized &&
            !string.Equals(route, AppConfig.Routes.RefreshToken, StringComparison.OrdinalIgnoreCase))
        {
            await RefreshAccessTokenAsync(accessToken!, cancellationToken);
            var refreshedToken = await SecureStorage.Default.GetAsync(AppConfig.Security.AccessTokenStorageKey);
            using var retryResponse = await SendHttpRequestAsync(method, route, payload, refreshedToken, cancellationToken);
            return await ReadEnvelopeAsync<T>(retryResponse, cancellationToken);
        }

        return await ReadEnvelopeAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendHttpRequestAsync(
        HttpMethod method, string route, object? payload, string? accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, route);
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (payload is not null)
            request.Content = JsonContent.Create(payload, options: JsonOptions);

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new ApiException("The API request timed out. Please try again.", HttpStatusCode.RequestTimeout, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Unable to connect to the API at {AppConfig.Api.BaseUrl}. Check that the API is running.", HttpStatusCode.ServiceUnavailable, ex);
        }
    }

    private async Task RefreshAccessTokenAsync(string expiredAccessToken, CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var currentAccessToken = await SecureStorage.Default.GetAsync(AppConfig.Security.AccessTokenStorageKey);
            // Another concurrent request may already have rotated the refresh token.
            if (!string.IsNullOrWhiteSpace(currentAccessToken) &&
                !string.Equals(currentAccessToken, expiredAccessToken, StringComparison.Ordinal))
                return;

            var refreshToken = await SecureStorage.Default.GetAsync(AppConfig.Security.RefreshTokenStorageKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ApiException("Your session has expired. Please sign in again.", HttpStatusCode.Unauthorized);

            using var request = new HttpRequestMessage(HttpMethod.Post, AppConfig.Routes.RefreshToken)
            {
                Content = JsonContent.Create(new { RefreshToken = refreshToken }, options: JsonOptions)
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new ApiException("Unable to refresh your session. Please try again.", HttpStatusCode.ServiceUnavailable, ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                ApiResponse<RefreshTokenPayload>? envelope;
                try { envelope = JsonSerializer.Deserialize<ApiResponse<RefreshTokenPayload>>(body, JsonOptions); }
                catch (JsonException ex)
                {
                    throw new ApiException("The refresh endpoint returned an invalid response.", HttpStatusCode.BadGateway, ex);
                }

                var data = envelope?.Data;
                if (!response.IsSuccessStatusCode || envelope?.Success != true ||
                    string.IsNullOrWhiteSpace(data?.AccessToken) || string.IsNullOrWhiteSpace(data.RefreshToken))
                {
                    SecureStorage.Default.Remove(AppConfig.Security.AccessTokenStorageKey);
                    SecureStorage.Default.Remove(AppConfig.Security.RefreshTokenStorageKey);
                    RestaurantSession.Clear();
                    throw new ApiException(
                        string.IsNullOrWhiteSpace(envelope?.Message)
                            ? "Your session has expired. Please sign in again."
                            : envelope.Message,
                        response.StatusCode == HttpStatusCode.OK ? HttpStatusCode.Unauthorized : response.StatusCode);
                }

                await SecureStorage.Default.SetAsync(AppConfig.Security.AccessTokenStorageKey, data.AccessToken);
                await SecureStorage.Default.SetAsync(AppConfig.Security.RefreshTokenStorageKey, data.RefreshToken);
            }
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static async Task<ApiResponse<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiResponse<T>? envelope = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions); }
            catch (JsonException ex)
            {
                throw new ApiException("The API returned an invalid response format.", HttpStatusCode.BadGateway, ex);
            }
        }

        if (!response.IsSuccessStatusCode || envelope?.Success != true)
        {
            var message = envelope?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = response.StatusCode == HttpStatusCode.Conflict
                    ? "The request conflicts with an existing record."
                    : $"API request failed ({(int)response.StatusCode}).";
            throw new ApiException(message, response.StatusCode);
        }

        return envelope;
    }

    private sealed class RefreshTokenPayload
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public Guid? RestaurantId { get; set; }
    }
}
