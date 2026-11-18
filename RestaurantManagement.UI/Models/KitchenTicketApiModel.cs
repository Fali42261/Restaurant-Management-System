namespace RestaurantManagement.UI.Models;

public sealed class KitchenTicketApiModel
{
    public Guid TicketId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = "New";
    public DateTime CreatedAt { get; set; }
    public string Items { get; set; } = string.Empty;
}

public sealed class PagedApiResult<T>
{
    public List<T> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}
