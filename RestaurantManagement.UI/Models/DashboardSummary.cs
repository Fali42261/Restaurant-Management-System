namespace RestaurantManagement.UI.Models;

public sealed class DashboardSummary
{
    public int TodayOrders { get; set; }
    public decimal TodaySales { get; set; }
    public int TodayReservations { get; set; }
    public int AvailableTables { get; set; }
    public int ActiveOrders { get; set; }
    public int TotalTables { get; set; }
    public int OccupiedTables { get; set; }
    public int UpcomingGuests { get; set; }
}
