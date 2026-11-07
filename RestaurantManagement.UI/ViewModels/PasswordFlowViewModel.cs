using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class PasswordFlowViewModel : BaseViewModel
{
    private string _email = "";
    private string _verificationCode = "";
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";
    private string _statusMessage = "";
    private CancellationTokenSource? _toastCts;

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
    public string StatusMessage { get => _statusMessage; private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public ICommand ResetPasswordCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand GoToLoginCommand { get; }

    private void ResetPassword()
    {
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        { ShowToast("Enter a valid email address."); return; }
        if (string.IsNullOrWhiteSpace(VerificationCode) || VerificationCode.Trim().Length < 4)
        { ShowToast("Enter the verification code."); return; }
        ValidateNewPassword("Password reset");
    }

    private void ChangePassword()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        { ShowToast("Enter your current password."); return; }
        ValidateNewPassword("Password change");
    }

    private void ValidateNewPassword(string action)
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        { ShowToast("New password must contain at least 8 characters."); return; }
        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        { ShowToast("New password and confirmation do not match."); return; }
        ShowToast($"{action} details validated. Connect the authentication API to complete this action.");
    }

    private async void ShowToast(string message)
    {
        _toastCts?.Cancel();
        _toastCts?.Dispose();
        _toastCts = new CancellationTokenSource();
        StatusMessage = message;
        try { await Task.Delay(3500, _toastCts.Token); StatusMessage = ""; }
        catch (OperationCanceledException) { }
    }
}