using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.UI.Services;

namespace RestaurantManagement.UI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            ThemeService.ApplySavedTheme();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}