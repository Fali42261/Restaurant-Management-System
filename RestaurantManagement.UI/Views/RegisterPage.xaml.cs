using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = new RegisterViewModel();
    }
}