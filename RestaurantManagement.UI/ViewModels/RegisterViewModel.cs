using System.Net.Mail;
using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class RegisterViewModel : BaseViewModel
{
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _mobileNumber = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;

    public RegisterViewModel()
    {
        Title = "Create account";
        RegisterCommand = new Command(Register);
        GoToLoginCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string MobileNumber
    {
        get => _mobileNumber;
        set => SetProperty(ref _mobileNumber, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
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

    public ICommand RegisterCommand { get; }
    public ICommand GoToLoginCommand { get; }

    private void Register()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            StatusMessage = "Enter your full name.";
            return;
        }

        if (!IsValidEmail(Email))
        {
            StatusMessage = "Enter a valid email address.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(MobileNumber) &&
            MobileNumber.Count(char.IsDigit) < 10)
        {
            StatusMessage = "Enter a valid mobile number or leave it blank.";
            return;
        }

        if (Password.Length < 8)
        {
            StatusMessage = "Password must contain at least 8 characters.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            StatusMessage = "Passwords do not match.";
            return;
        }

        StatusMessage = "Your details are valid. Registration storage and authentication are not connected yet.";
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            var address = new MailAddress(value.Trim());
            return address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}