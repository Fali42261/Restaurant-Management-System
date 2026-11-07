using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel();
    }

    private void TogglePasswordVisibility(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        if (sender is Button button) button.Text = PasswordEntry.IsPassword ? "👁 Show" : "🙈 Hide";
    }
}