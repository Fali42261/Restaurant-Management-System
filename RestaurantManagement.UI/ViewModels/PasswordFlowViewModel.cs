using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class PasswordFlowViewModel : BaseViewModel
{
    private string _email = "";
    private string _verificationCode = "";
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";

    public PasswordFlowViewModel()
    {
        Title = "Password";
        ResetPasswordCommand = new Command(ResetPassword);
        ChangePasswordCommand = new Command(ChangePassword);
        GoToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//LoginPage"));
    }

    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string VerificationCode { get => _verificationCode; set => SetProperty(ref _verificationCode, value); }
    public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public ICommand ResetPasswordCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand GoToLoginCommand { get; }

    private void ResetPassword()
    {
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        return;
        if (string.IsNullOrWhiteSpace(VerificationCode) || VerificationCode.Trim().Length < 4)
        return;
        ValidateNewPassword("Password reset");
    }

    private void ChangePassword()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        return;
        ValidateNewPassword("Password change");
    }

    private void ValidateNewPassword(string action)
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        return;
        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        return;
        // Validation only; feedback notifications are temporarily disabled.
    }

}
