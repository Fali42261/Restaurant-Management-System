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
        PasswordEyeButton.Text = PasswordEntry.IsPassword ? "Show" : "Hide";
    }

    private void ToggleConfirmPasswordVisibility(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEyeButton.Text = ConfirmPasswordEntry.IsPassword ? "Show" : "Hide";
    }
}