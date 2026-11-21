namespace RestaurantManagement.UI.Models.Responses;

public sealed class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public string? TraceId { get; set; }
}

public sealed class ApiException(string message, System.Net.HttpStatusCode statusCode) : Exception(message)
{
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;
}
