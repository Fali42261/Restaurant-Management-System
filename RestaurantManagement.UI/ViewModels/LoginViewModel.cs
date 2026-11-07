using System.Windows.Input;
using RestaurantManagement.UI.Views;

namespace RestaurantManagement.UI.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private string _emailOrMobile="";
    private string _password="";
    private string _statusMessage="";
    private CancellationTokenSource? _toastCts;
    public LoginViewModel()
    {
        Title="Sign in";
        SignInCommand=new Command(async()=>await SignInAsync());
        GoToRegisterCommand=new Command(async()=>await Shell.Current.GoToAsync(nameof(RegisterPage)));
        GoToResetPasswordCommand=new Command(async()=>await Shell.Current.GoToAsync(nameof(ResetPasswordPage)));
        GoToChangePasswordCommand=new Command(async()=>await Shell.Current.GoToAsync(nameof(ChangePasswordPage)));
    }
    public string EmailOrMobile { get=>_emailOrMobile; set=>SetProperty(ref _emailOrMobile,value); }
    public string Password { get=>_password; set=>SetProperty(ref _password,value); }
    public string StatusMessage { get=>_statusMessage; private set { if(SetProperty(ref _statusMessage,value)) OnPropertyChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage=>!string.IsNullOrWhiteSpace(StatusMessage);
    public ICommand SignInCommand{get;}
    public ICommand GoToRegisterCommand{get;}
    public ICommand GoToResetPasswordCommand{get;}
    public ICommand GoToChangePasswordCommand{get;}
    private async Task SignInAsync()
    {
        if(string.IsNullOrWhiteSpace(EmailOrMobile)) { ShowToast("Enter your email address or mobile number."); return; }
        if(string.IsNullOrWhiteSpace(Password)) { ShowToast("Enter your password."); return; }
        if(Password.Length<8) { ShowToast("Invalid password. Use at least 8 characters."); return; }
        // UI preview only: authentication will be connected in the API/database phase.
        StatusMessage="";
        await Shell.Current.GoToAsync(nameof(DashboardPage));
    }
    private async void ShowToast(string message)
    {
        _toastCts?.Cancel(); _toastCts?.Dispose(); _toastCts=new CancellationTokenSource();
        StatusMessage=message;
        try { await Task.Delay(3000,_toastCts.Token); StatusMessage=""; } catch(OperationCanceledException) { }
    }
}