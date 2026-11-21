using System.Net.Mail;
using System.Net.Http;
using System.Windows.Input;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.Services.Interfaces;

namespace RestaurantManagement.UI.ViewModels;

public sealed class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _mobileNumber = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isRegisterEnabled = true;

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Create owner account";
        RegisterCommand = new Command(async () => await RegisterAsync());
        GoToLoginCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string MobileNumber { get => _mobileNumber; set => SetProperty(ref _mobileNumber, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public bool IsRegisterEnabled { get => _isRegisterEnabled; private set => SetProperty(ref _isRegisterEnabled, value); }
    public ICommand RegisterCommand { get; }
    public ICommand GoToLoginCommand { get; }

    private async Task RegisterAsync()
    {
        if (IsBusy) return;

        var validationMessage = ValidateInput();
        if (validationMessage is not null)
        {
            ToastService.Error(validationMessage);
            return;
        }

        try
        {
            IsBusy = true;
            IsRegisterEnabled = false;
            var message = await _authService.RegisterOwnerAsync(new OwnerRegistrationRequest(
                FullName.Trim(),
                Email.Trim().ToLowerInvariant(),
                MobileNumber.Trim(),
                Password));

            ToastService.Success(message + " Please sign in.");
            await Task.Delay(TimeSpan.FromSeconds(5));
            await Shell.Current.GoToAsync("..");
        }
        catch (AuthApiException ex)
        {
            ToastService.Error(ex.Message);
        }
        catch (HttpRequestException)
        {
            ToastService.Error($"Cannot reach the API at {Configuration.AppConfig.Api.BaseUrl}. Start the API and check the configured base URL.");
        }
        catch (TaskCanceledException)
        {
            ToastService.Error("The API did not respond in time. Please try again.");
        }
        finally
        {
            IsBusy = false;
            IsRegisterEnabled = true;
        }
    }

    private string? ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(FullName)) return "Enter your full name.";
        if (!IsValidEmail(Email)) return "Enter a valid email address.";
        var mobile = MobileNumber?.Trim() ?? string.Empty;
        var normalizedMobile = mobile.StartsWith("+", StringComparison.Ordinal) ? mobile[1..] : mobile;
        if (normalizedMobile.Length is < 10 or > 15 || !normalizedMobile.All(char.IsDigit))
            return "Enter a valid mobile number with 10 to 15 digits.";
        if (Password.Length < 8 || !Password.Any(char.IsUpper) || !Password.Any(char.IsLower) ||
            !Password.Any(char.IsDigit) || !Password.Any(c => !char.IsLetterOrDigit(c)))
            return "Password must contain at least 8 characters with uppercase, lowercase, number and symbol."; 

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            return "Password and confirm password do not match.";
        return null;
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
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
