using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage()
    {
        InitializeComponent();
        BindingContext = new PasswordFlowViewModel(new RestaurantManagement.UI.Services.AuthService());
    }

    private void ToggleCurrentPassword(object sender, EventArgs e)
    {
        CurrentPasswordEntry.IsPassword = !CurrentPasswordEntry.IsPassword;
        CurrentPasswordEyeButton.Source = CurrentPasswordEntry.IsPassword ? "eye.png" : "eye_off.png";
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