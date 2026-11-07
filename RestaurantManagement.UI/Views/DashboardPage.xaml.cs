using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = new DashboardViewModel();
    }
}