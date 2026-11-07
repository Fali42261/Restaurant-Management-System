namespace RestaurantManagement.UI.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string Role { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
}
