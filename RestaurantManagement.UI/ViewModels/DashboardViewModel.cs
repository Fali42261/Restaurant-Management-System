using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    public DashboardViewModel()
    {
        Title = "Dashboard";
        LogoutCommand = new Command(async () =>
            await Shell.Current.GoToAsync("//LoginPage"));
    }

    public ICommand LogoutCommand { get; }
}