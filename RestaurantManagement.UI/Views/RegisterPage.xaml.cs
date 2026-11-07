using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = new RegisterViewModel();
    }

    private void TogglePasswordVisibility(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        PasswordEyeButton.Source = PasswordEntry.IsPassword ? "eye.svg" : "eye_off.svg";
    }

    private void ToggleConfirmPasswordVisibility(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEyeButton.Source = ConfirmPasswordEntry.IsPassword ? "eye.svg" : "eye_off.svg";
    }
}