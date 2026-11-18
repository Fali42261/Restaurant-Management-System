namespace RestaurantManagement.UI.Models;

public sealed class DashboardSalesPoint
{
    public DateTime SaleDate { get; set; }
    public int BillCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Tax { get; set; }
}

public sealed class DashboardTopProduct
{
    public string Name { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Sales { get; set; }
}
