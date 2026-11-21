using System.Windows.Input;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Models.Responses;
using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.Services.Interfaces;
using RestaurantManagement.UI.Validation;

namespace RestaurantManagement.UI.ViewModels;

public sealed class PasswordFlowViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _email = "";
    private string _verificationCode = "";
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";

    public PasswordFlowViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Password";
        ForgotPasswordCommand = new Command(async () => await SendResetCodeAsync());
        ResetPasswordCommand = new Command(async () => await ResetPasswordAsync());
        ChangePasswordCommand = new Command(async () => await ChangePasswordAsync());
        GoToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//LoginPage"));
    }

    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string VerificationCode { get => _verificationCode; set => SetProperty(ref _verificationCode, value); }
    public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public ICommand ForgotPasswordCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand GoToLoginCommand { get; }

    private async Task SendResetCodeAsync()
    {
        if (IsBusy) return;
        var validation = AuthRequestValidator.ValidateForgot(Email);
        if (validation is not null) { ToastService.Error(validation); return; }

        await RunRequestAsync(async () =>
        {
            var message = await _authService.ForgotPasswordAsync(new ForgotPasswordRequest(Email.Trim()));
            ToastService.Success(message);
        });
    }

    private async Task ResetPasswordAsync()
    {
        if (IsBusy) return;
        var validation = AuthRequestValidator.ValidateReset(Email, VerificationCode, NewPassword, ConfirmPassword);
        if (validation is not null) { ToastService.Error(validation); return; }

        await RunRequestAsync(async () =>
        {
            var message = await _authService.ResetPasswordAsync(
                new ResetPasswordRequest(Email.Trim(), VerificationCode.Trim(), NewPassword));
            ToastService.Success(message);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }

    private async Task ChangePasswordAsync()
    {
        if (IsBusy) return;
        var validation = AuthRequestValidator.ValidateChange(CurrentPassword, NewPassword, ConfirmPassword);
        if (validation is not null) { ToastService.Error(validation); return; }

        await RunRequestAsync(async () =>
        {
            var message = await _authService.ChangePasswordAsync(new ChangePasswordRequest(CurrentPassword, NewPassword));
            ToastService.Success(message);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await Shell.Current.GoToAsync("//LoginPage");
        });
    }

    private async Task RunRequestAsync(Func<Task> request)
    {
        try
        {
            IsBusy = true;
            await request();
        }
        catch (ApiException ex)
        {
            ToastService.Error(ex.Message);
        }
        catch (HttpRequestException)
        {
            ToastService.Error($"Cannot reach the API at {Configuration.AppConfig.Api.BaseUrl}. Check that the API is running.");
        }
        catch (TaskCanceledException)
        {
            ToastService.Error("The API did not respond in time. Please try again.");
        }
        catch (Exception ex)
        {
            ToastService.Error($"Request failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
