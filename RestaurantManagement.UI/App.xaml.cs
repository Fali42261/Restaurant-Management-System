using Microsoft.Extensions.DependencyInjection;

namespace RestaurantManagement.UI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();\n            ThemeService.ApplySavedTheme();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}