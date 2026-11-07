using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage()
    {
        InitializeComponent();
        BindingContext = new PasswordFlowViewModel();
    }

    private void ToggleCurrentPassword(object sender, EventArgs e)
    {
        CurrentPasswordEntry.IsPassword = !CurrentPasswordEntry.IsPassword;
        CurrentPasswordEyeButton.Source = CurrentPasswordEntry.IsPassword ? "eye.svg" : "eye_off.svg";
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