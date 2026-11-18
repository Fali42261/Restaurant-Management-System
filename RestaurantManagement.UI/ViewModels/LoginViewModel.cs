using System.Net.Http;
using System.Windows.Input;
using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.Services.Interfaces;
using RestaurantManagement.UI.Views;

namespace RestaurantManagement.UI.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _emailOrMobile = string.Empty;
    private string _password = string.Empty;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Sign in";
        SignInCommand = new Command(async () => await SignInAsync());
        GoToRegisterCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(RegisterPage)));
        GoToResetPasswordCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(ResetPasswordPage)));
        GoToChangePasswordCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(ChangePasswordPage)));
    }

    public string EmailOrMobile { get => _emailOrMobile; set => SetProperty(ref _emailOrMobile, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public ICommand SignInCommand { get; }
    public ICommand GoToRegisterCommand { get; }
    public ICommand GoToResetPasswordCommand { get; }
    public ICommand GoToChangePasswordCommand { get; }

    private async Task SignInAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(EmailOrMobile))
        {
            await Shell.Current.DisplayAlert("Sign in", "Enter your email or mobile number.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            await Shell.Current.DisplayAlert("Sign in", "Enter your password.", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            var user = await _authService.SignInAsync(EmailOrMobile, Password);
            var restaurants = await _authService.GetRestaurantsAsync(user.Id);
            if (restaurants.Count == 0)
            {
                if (!string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    await Shell.Current.DisplayAlert("Restaurant not assigned", "Your account is not assigned to an active restaurant. Please contact the owner.", "OK");
                    return;
                }

                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(new CreateRestaurantPage(user)));
                return;
            }

            // API response ordering is authoritative: the first restaurant is selected by default.
            var selected = restaurants[0];
            await _authService.SelectRestaurantAsync(selected);
            RestaurantSession.Set(user, restaurants, selected);
            TrialAccessService.StartTrialIfNeeded();
            await Shell.Current.GoToAsync(nameof(DashboardPage));
        }
        catch (AuthApiException ex)
        {
            await Shell.Current.DisplayAlert("Sign in failed", ex.Message, "OK");
        }
        catch (HttpRequestException)
        {
            await Shell.Current.DisplayAlert("Connection error",
                $"Cannot reach the API at {Configuration.AppConfig.Api.BaseUrl}. Start the API and check the configured base URL.",
                "OK");
        }
        catch (TaskCanceledException)
        {
            await Shell.Current.DisplayAlert("Request timeout", "The API did not respond in time. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
