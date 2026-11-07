using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel();
    }
}