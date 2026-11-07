using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class ResetPasswordPage : ContentPage
{
    public ResetPasswordPage()
    {
        InitializeComponent();
        BindingContext = new PasswordFlowViewModel();
    }

    private void ToggleNewPassword(object sender, EventArgs e)
    {
        NewPasswordEntry.IsPassword = !NewPasswordEntry.IsPassword;
        NewPasswordEyeButton.Source = NewPasswordEntry.IsPassword ? "eye.svg" : "eye_off.svg";
    }

    private void ToggleConfirmPassword(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEyeButton.Source = ConfirmPasswordEntry.IsPassword ? "eye.svg" : "eye_off.svg";
    }
}