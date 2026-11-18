using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using RestaurantManagement.UI.Configuration;

namespace RestaurantManagement.UI.Services;

/// <summary>
/// Restaurant-scoped Orders and POS API client. The active restaurant is carried by
/// the access token selected during sign-in; never send a client-supplied RestaurantId.
/// </summary>
public sealed class OrdersPosApiService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(AppConfig.Api.BaseUrl, UriKind.Absolute),
        Timeout = TimeSpan.FromSeconds(AppConfig.Application.RequestTimeoutSeconds)
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<PagedResult<ProductDto>?> GetProductsAsync(CancellationToken ct = default) =>
        SendAsync<PagedResult<ProductDto>>(HttpMethod.Get, "api/v1/products?pageNumber=1&pageSize=100", null, ct);

    public Task<PagedResult<OrderDto>?> GetOrdersAsync(CancellationToken ct = default) =>
        SendAsync<PagedResult<OrderDto>>(HttpMethod.Get, "api/v1/orders?pageNumber=1&pageSize=100", null, ct);

    public Task<PagedResult<BillDto>?> GetBillsAsync(CancellationToken ct = default) =>
        SendAsync<PagedResult<BillDto>>(HttpMethod.Get, "api/v1/bills?pageNumber=1&pageSize=100", null, ct);

    public Task<List<TableDto>?> GetTablesAsync(CancellationToken ct = default) =>
        SendAsync<List<TableDto>>(HttpMethod.Get, "api/v1/tables", null, ct);

    public Task<CheckoutResult?> CheckoutAsync(PosCheckoutRequest request, CancellationToken ct = default) =>
        SendAsync<CheckoutResult>(HttpMethod.Post, "api/v1/pos/checkout", request, ct);

    public Task<object?> CreateBillAsync(CreateBillApiRequest request, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, "api/v1/bills", request, ct);

    public Task ReplaceOrderItemsAsync(Guid orderId, List<ReplaceOrderItem> items, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Put, $"api/v1/orders/{orderId}/items", new { Items = items }, ct);

    public async Task MarkBillPaidAsync(Guid billId, CancellationToken ct = default)
    {
        if (billId == Guid.Empty) throw new ArgumentException("A valid bill is required.", nameof(billId));
        await SendAsync<object>(HttpMethod.Put, $"api/v1/pos/bills/{billId}/pay", null, ct);
    }

    public async Task DeleteOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("A valid order is required.", nameof(orderId));
        await SendAsync<object>(HttpMethod.Delete, $"api/v1/orders/{orderId}", null, ct);
    }

    private static async Task<T?> SendAsync<T>(HttpMethod method, string route, object? payload, CancellationToken ct)
    {
        var token = await SecureStorage.Default.GetAsync("swapDigit.accessToken");
        if (string.IsNullOrWhiteSpace(token))
            throw new OrdersPosApiException("Please sign in again.", HttpStatusCode.Unauthorized);

        using var request = new HttpRequestMessage(method, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (payload is not null) request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await Client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        ApiEnvelope<T>? envelope = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions); }
            catch (JsonException) { }
        }

        if (!response.IsSuccessStatusCode || envelope?.Success != true)
            throw new OrdersPosApiException(
                envelope?.Message ?? $"Orders/POS API request failed ({(int)response.StatusCode}).",
                response.StatusCode);
        return envelope.Data;
    }

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}

public sealed class OrdersPosApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public sealed class ProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public sealed class OrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string? CustomerName { get; set; }
    public string OrderType { get; set; } = "";
    public Guid? TableId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "";
    public string ItemSummary { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BillDto
{
    public Guid BillId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public Guid? OrderId { get; set; }
    public string CustomerName { get; set; } = "";
    public string OrderType { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "";
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class TableDto
{
    public Guid TableId { get; set; }
    public string Name { get; set; } = "";
    public int Seats { get; set; }
    public string Status { get; set; } = "";
}

public sealed record PosCheckoutRequest(
    string? CustomerName,
    string OrderType,
    Guid? TableId,
    decimal Discount,
    decimal Tax,
    string PaymentMethod,
    decimal CashReceived,
    List<PosCheckoutItem> Items);

public sealed record PosCheckoutItem(Guid ProductId, int Quantity, string? Notes);

public sealed class CheckoutResult
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "";
}

public sealed record ReplaceOrderItem(Guid ProductId, int Quantity, string? Notes = null);

public sealed record CreateBillApiRequest(Guid? OrderId, string CustomerName, string OrderType, decimal Subtotal, decimal Discount, decimal Tax, decimal Total, string PaymentMethod, decimal AmountPaid);
