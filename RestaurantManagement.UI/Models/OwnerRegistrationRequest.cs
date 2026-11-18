namespace RestaurantManagement.UI.Models;

public sealed record OwnerRegistrationRequest(
    string FullName,
    string Email,
    string MobileNumber,
    string Password);
