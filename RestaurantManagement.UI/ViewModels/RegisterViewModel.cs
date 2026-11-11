using System.Net.Mail;
using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class RegisterViewModel : BaseViewModel
{
    private string _fullName="",_email="",_mobileNumber="",_password="",_confirmPassword="";
    public RegisterViewModel()
    {
        Title="Create owner account";
        RegisterCommand=new Command(Register);
        GoToLoginCommand=new Command(async()=>await Shell.Current.GoToAsync(".."));
    }
    public string FullName { get=>_fullName; set=>SetProperty(ref _fullName,value); }
    public string Email { get=>_email; set=>SetProperty(ref _email,value); }
    public string MobileNumber { get=>_mobileNumber; set=>SetProperty(ref _mobileNumber,value); }
    public string Password { get=>_password; set=>SetProperty(ref _password,value); }
    public string ConfirmPassword { get=>_confirmPassword; set=>SetProperty(ref _confirmPassword,value); }
    public ICommand RegisterCommand{get;}
    public ICommand GoToLoginCommand{get;}
    private void Register()
    {
        if(string.IsNullOrWhiteSpace(FullName)) return;
        if(!IsValidEmail(Email)) return;
        if(string.IsNullOrWhiteSpace(MobileNumber)||MobileNumber.Count(char.IsDigit)<10) return;
        if(Password.Length<8) return;
        if(!string.Equals(Password,ConfirmPassword,StringComparison.Ordinal)) return;
        // Validation only; feedback notifications are temporarily disabled.
    }
    private static bool IsValidEmail(string value)
    {
        if(string.IsNullOrWhiteSpace(value)) return false;
        try { var address=new MailAddress(value.Trim()); return address.Address.Equals(value.Trim(),StringComparison.OrdinalIgnoreCase); }
        catch(FormatException) { return false; }
    }
}