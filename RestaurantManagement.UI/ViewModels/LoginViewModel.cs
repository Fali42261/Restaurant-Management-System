using System.Net.Http;
using System.Windows.Input;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Models.Responses;
using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.Services.Interfaces;
using RestaurantManagement.UI.Views;
using RestaurantManagement.UI.Validation;

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
        var validationMessage = AuthRequestValidator.ValidateLogin(EmailOrMobile, Password);
        if (validationMessage is not null) { ToastService.Error(validationMessage); return; }

        try
        {
            IsBusy = true;
            var request = new LoginRequest(EmailOrMobile.Trim(), Password);
            var user = await _authService.SignInAsync(request);
            var restaurants = await _authService.GetRestaurantsAsync(user.Id);
            if (restaurants.Count == 0)
            {
                if (!string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    ToastService.Error("Your account is not assigned to an active restaurant. Please contact the owner.");
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
            ToastService.Success("Signed in successfully.");
        }
        catch (ApiException ex)
        {
            ToastService.Error(ex.Message);
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
        catch (Exception ex)
        {
            ToastService.Error($"Sign in failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
