using RestaurantManagement.UI.Views;

namespace RestaurantManagement.UI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(DashboardPage), typeof(DashboardPage));
    }
}