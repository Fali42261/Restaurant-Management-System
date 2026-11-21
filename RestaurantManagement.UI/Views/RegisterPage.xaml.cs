using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = new RegisterViewModel(new AuthService());
    }

    private void TogglePasswordVisibility(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        PasswordEyeButton.Source = PasswordEntry.IsPassword ? "eye.png" : "eye_off.png";
    }

    private void ToggleConfirmPasswordVisibility(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEyeButton.Source = ConfirmPasswordEntry.IsPassword ? "eye.png" : "eye_off.png";
    }
}
