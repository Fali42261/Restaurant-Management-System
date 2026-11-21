using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class ResetPasswordPage : ContentPage
{
    public ResetPasswordPage()
    {
        InitializeComponent();
        BindingContext = new PasswordFlowViewModel(new RestaurantManagement.UI.Services.AuthService());
    }

    private void ToggleNewPassword(object sender, EventArgs e)
    {
        NewPasswordEntry.IsPassword = !NewPasswordEntry.IsPassword;
        NewPasswordEyeButton.Source = NewPasswordEntry.IsPassword ? "eye.png" : "eye_off.png";
    }

    private void ToggleConfirmPassword(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEyeButton.Source = ConfirmPasswordEntry.IsPassword ? "eye.png" : "eye_off.png";
    }
}