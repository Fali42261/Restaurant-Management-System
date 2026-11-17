namespace RestaurantManagement.API.Models;

public sealed record OwnerRegistration(string FullName, string Email, string MobileNumber, string Password);
public sealed record LoginRequest(string EmailOrMobile, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record CreateProductRequest(string Name, string Category, string? Description, decimal Price, string? ImageUrl);
public sealed record CreateFloorRequest(string Name);
public sealed record CreateTableRequest(string Name, int Seats, Guid FloorId);
public sealed record CreateOrderRequest(string? CustomerName, string OrderType, Guid? TableId, decimal Discount, decimal Tax, string PaymentMethod, decimal CashReceived, List<OrderItemRequest> Items);
public sealed record OrderItemRequest(Guid ProductId, int Quantity, decimal UnitPrice, string? Notes);
public sealed record CreateReservationRequest(string GuestName, string MobileNumber, DateTime ReservationDate, TimeSpan ReservationTime, int GuestCount, Guid? TableId, string? Notes);
public sealed record CreateBillRequest(Guid? OrderId, string CustomerName, string OrderType, decimal Subtotal, decimal Discount, decimal Tax, decimal Total, string PaymentMethod, decimal AmountPaid);
public sealed record UpdateStatusRequest(string Status);
public sealed record CreateInventoryItemRequest(string Name, string Unit, decimal OpeningStock, decimal ReorderLevel, decimal CostPrice);
public sealed record StockMovementRequest(Guid ItemId, string MovementType, decimal Quantity, string? Reference, string? Notes);
public sealed record CreateSupportTicketRequest(string Subject, string Message, string Priority);
public sealed record UpdateStaffRequest(string FullName, string Email, string MobileNumber, string Role, bool IsActive);
