using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using RestaurantManagement.UI.Configuration;
using RestaurantManagement.UI.Models.Responses;

namespace RestaurantManagement.UI.Services.Api;

/// <summary>Shared HTTP pipeline for all UI-to-API requests.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri(AppConfig.Api.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(AppConfig.Application.RequestTimeoutSeconds)
        };
    }

    public async Task<T?> GetAsync<T>(string route, bool authorized = false, CancellationToken cancellationToken = default) =>
        await SendAsync<T>(HttpMethod.Get, route, null, authorized, cancellationToken);

    public async Task<T?> PostAsync<T>(string route, object? payload = null, bool authorized = false, CancellationToken cancellationToken = default) =>
        await SendAsync<T>(HttpMethod.Post, route, payload, authorized, cancellationToken);

    public async Task<T?> PutAsync<T>(string route, object? payload = null, bool authorized = false, CancellationToken cancellationToken = default) =>
        await SendAsync<T>(HttpMethod.Put, route, payload, authorized, cancellationToken);

    public async Task<T?> DeleteAsync<T>(string route, bool authorized = false, CancellationToken cancellationToken = default) =>
        await SendAsync<T>(HttpMethod.Delete, route, null, authorized, cancellationToken);

    public async Task<string> PostMessageAsync(string route, object payload, bool authorized = false, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        if (authorized)
        {
            var token = await SecureStorage.Default.GetAsync("swapDigit.accessToken");
            if (string.IsNullOrWhiteSpace(token))
                throw new ApiException("Please sign in again.", HttpStatusCode.Unauthorized);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiResponse<JsonElement>? envelope = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOptions); }
            catch (JsonException) { }
        }
        if (!response.IsSuccessStatusCode || envelope?.Success != true)
            throw new ApiException(envelope?.Message ?? $"API request failed ({(int)response.StatusCode}).", response.StatusCode);
        return string.IsNullOrWhiteSpace(envelope.Message) ? "Request completed successfully." : envelope.Message;
    }

    public async Task<T?> SendAsync<T>(HttpMethod method, string route, object? payload = null, bool authorized = false, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, route);
        if (authorized)
        {
            var token = await SecureStorage.Default.GetAsync("swapDigit.accessToken");
            if (string.IsNullOrWhiteSpace(token))
                throw new ApiException("Please sign in again.", HttpStatusCode.Unauthorized);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (payload is not null)
            request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiResponse<T>? envelope = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions); }
            catch (JsonException) { /* Report a safe API error below. */ }
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

        return envelope.Data;
    }
}
