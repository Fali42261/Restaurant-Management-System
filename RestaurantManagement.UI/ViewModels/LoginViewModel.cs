using System.Windows.Input;
using RestaurantManagement.UI.Views;

namespace RestaurantManagement.UI.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private string _emailOrMobile="";
    private string _password="";
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
    public ICommand SignInCommand{get;}
    public ICommand GoToRegisterCommand{get;}
    public ICommand GoToResetPasswordCommand{get;}
    public ICommand GoToChangePasswordCommand{get;}
    private async Task SignInAsync()
    {
        if(string.IsNullOrWhiteSpace(EmailOrMobile)) return;
        if(string.IsNullOrWhiteSpace(Password)) return;
        if(Password.Length<8) return;
        // UI preview only: authentication will be connected in the API/database phase.
        await Shell.Current.GoToAsync(nameof(DashboardPage));
    }
}
