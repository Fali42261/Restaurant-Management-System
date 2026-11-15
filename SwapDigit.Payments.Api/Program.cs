using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("razorpay", client =>
{
    client.BaseAddress = new Uri("https://api.razorpay.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ConcurrentDictionary<string, PendingPayment>>();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "swapDigit Payments API", provider = "Razorpay", environment = app.Environment.EnvironmentName }));

app.MapPost("/api/v1/payments/orders", async (CreateOrderRequest request, IConfiguration config,
    IHttpClientFactory clients, ConcurrentDictionary<string, PendingPayment> pending, CancellationToken ct) =>
{
    if (request.AmountPaise < 100 || request.AmountPaise > 10_000_000_00)
        return Results.BadRequest(new { error = "Amount must be between ₹1 and ₹1 crore." });
    if (string.IsNullOrWhiteSpace(request.Receipt) || request.Receipt.Length > 40)
        return Results.BadRequest(new { error = "A receipt reference (max 40 characters) is required." });

    var keyId = config["Razorpay:KeyId"] ?? config["RAZORPAY_KEY_ID"];
    var keySecret = config["Razorpay:KeySecret"] ?? config["RAZORPAY_KEY_SECRET"];
    if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
        return Results.Problem("Razorpay credentials are not configured on the server.", statusCode: 503);

    var client = clients.CreateClient("razorpay");
    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    using var response = await client.PostAsJsonAsync("orders", new
    {
        amount = request.AmountPaise,
        currency = "INR",
        receipt = request.Receipt,
        notes = new { source = "swapDigit-pos", order_type = request.OrderType ?? "POS" }
    }, ct);
    var body = await response.Content.ReadAsStringAsync(ct);
    if (!response.IsSuccessStatusCode)
        return Results.Problem("Razorpay could not create the order.", statusCode: 502, detail: body);

    using var json = JsonDocument.Parse(body);
    var orderId = json.RootElement.GetProperty("id").GetString()!;
    pending[orderId] = new PendingPayment(request.AmountPaise, request.Receipt, DateTimeOffset.UtcNow);
    return Results.Ok(new { keyId, orderId, amount = request.AmountPaise, currency = "INR", receipt = request.Receipt });
});

app.MapPost("/api/v1/payments/verify", (VerifyPaymentRequest request, IConfiguration config,
    ConcurrentDictionary<string, PendingPayment> pending) =>
{
    if (!pending.TryGetValue(request.OrderId, out var order))
        return Results.NotFound(new { error = "Order not found or expired. Create a new payment order." });

    var secret = config["Razorpay:KeySecret"] ?? config["RAZORPAY_KEY_SECRET"];
    if (string.IsNullOrWhiteSpace(secret))
        return Results.Problem("Razorpay secret is not configured on the server.", statusCode: 503);

    var expected = HmacHex(secret, $"{request.OrderId}|{request.PaymentId}");
    if (!FixedTimeEquals(expected, request.Signature))
        return Results.Unauthorized();

    pending.TryRemove(request.OrderId, out _);
    return Results.Ok(new { verified = true, orderId = request.OrderId, paymentId = request.PaymentId, amount = order.AmountPaise, currency = "INR" });
});

app.MapPost("/api/v1/payments/webhook", async (HttpRequest request, IConfiguration config, CancellationToken ct) =>
{
    var webhookSecret = config["Razorpay:WebhookSecret"] ?? config["RAZORPAY_WEBHOOK_SECRET"];
    if (string.IsNullOrWhiteSpace(webhookSecret))
        return Results.Problem("Razorpay webhook secret is not configured.", statusCode: 503);

    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(ct);
    var supplied = request.Headers["X-Razorpay-Signature"].ToString();
    var expected = HmacHex(webhookSecret, payload);
    if (!FixedTimeEquals(expected, supplied)) return Results.Unauthorized();

    // Persist verified webhook events to a durable database before using them
    // to reconcile POS invoices in production.
    return Results.Ok(new { received = true });
});

app.Run();

static string HmacHex(string secret, string value)
{
    var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(hash).ToLowerInvariant();
}
static bool FixedTimeEquals(string expected, string actual)
{
    try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)); }
    catch { return false; }
}

record CreateOrderRequest(long AmountPaise, string Receipt, string? OrderType);
record VerifyPaymentRequest(string OrderId, string PaymentId, string Signature);
record PendingPayment(long AmountPaise, string Receipt, DateTimeOffset CreatedAt);
