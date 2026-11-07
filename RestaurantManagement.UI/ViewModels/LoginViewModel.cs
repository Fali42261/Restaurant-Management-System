using System.Windows.Input;
using RestaurantManagement.UI.Views;

namespace RestaurantManagement.UI.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private string _emailOrMobile = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = string.Empty;

    public LoginViewModel()
    {
        Title = "Sign in";
        SignInCommand = new Command(async () => await SignInAsync());
        GoToRegisterCommand = new Command(async () =>
            await Shell.Current.GoToAsync(nameof(RegisterPage)));
    }

    public string EmailOrMobile
    {
        get => _emailOrMobile;
        set => SetProperty(ref _emailOrMobile, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ICommand SignInCommand { get; }
    public ICommand GoToRegisterCommand { get; }

    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(EmailOrMobile))
        {
            StatusMessage = "Enter your email address or mobile number.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Enter your password.";
            return;
        }

        // UI phase only: accept non-empty demo credentials and open the dashboard.
        // Real authentication will be connected in the API/database phase.
        StatusMessage = string.Empty;
        await Shell.Current.GoToAsync(nameof(DashboardPage));
    }
}