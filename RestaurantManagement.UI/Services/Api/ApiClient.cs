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
        using var request = new HttpRequestMessage(method, route);
        if (authorized)
        {
            var token = await SecureStorage.Default.GetAsync(AppConfig.Security.AccessTokenStorageKey);
            if (string.IsNullOrWhiteSpace(token))
                throw new ApiException("Please sign in again.", HttpStatusCode.Unauthorized);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (payload is not null)
            request.Content = JsonContent.Create(payload, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
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

        using (response)
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
    }
}
