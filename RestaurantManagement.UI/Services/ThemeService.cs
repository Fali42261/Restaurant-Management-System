namespace RestaurantManagement.UI.Services;

public static class ThemeService
{
    private const string ThemePreferenceKey = "swapdigit_app_theme";

    public static void ApplySavedTheme()
    {
        var saved = Preferences.Get(ThemePreferenceKey, "System");
        Application.Current!.UserAppTheme = saved switch
        {
            "Dark" => AppTheme.Dark,
            "Light" => AppTheme.Light,
            _ => AppTheme.Unspecified
        };
    }

    public static void SetTheme(AppTheme theme)
    {
        Preferences.Set(ThemePreferenceKey, theme switch
        {
            AppTheme.Dark => "Dark",
            AppTheme.Light => "Light",
            _ => "System"
        });

        if (Application.Current is not null)
            Application.Current.UserAppTheme = theme;
    }
}
