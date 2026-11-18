namespace RestaurantManagement.UI.Models;

public sealed class RestaurantInfo
{
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string? Address { get; set; }
    public string? OpeningTime { get; set; }
    public string? ClosingTime { get; set; }
    public string? OperatingDays { get; set; }
    public string? Role { get; set; }
}

public static class RestaurantSession
{
    public static User? User { get; private set; }
    public static List<RestaurantInfo> Restaurants { get; private set; } = [];
    public static RestaurantInfo? SelectedRestaurant { get; private set; }

    public static void Set(User user, IEnumerable<RestaurantInfo> restaurants, RestaurantInfo? selected)
    {
        User = user;
        Restaurants = restaurants.ToList();
        SelectedRestaurant = selected;
    }

    public static void Select(RestaurantInfo restaurant) => SelectedRestaurant = restaurant;

    public static void Clear()
    {
        User = null;
        Restaurants = [];
        SelectedRestaurant = null;
    }
}

public sealed record CreateRestaurantInput(
    string Name,
    string MobileNumber,
    string Address,
    TimeSpan OpeningTime,
    TimeSpan ClosingTime,
    string[] OperatingDays);
