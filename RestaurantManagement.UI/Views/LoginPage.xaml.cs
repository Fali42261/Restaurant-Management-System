using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(new AuthService());
    }

    private void TogglePasswordVisibility(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        PasswordEyeButton.Source = PasswordEntry.IsPassword ? "eye.svg" : "eye_off.svg";
    }
}
