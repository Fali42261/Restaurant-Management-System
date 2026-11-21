using Microsoft.Maui.Controls;
using RestaurantManagement.UI.Configuration;

namespace RestaurantManagement.UI.Services;

/// <summary>Displays a non-blocking five-second notification in the top-right corner.</summary>
public static class ToastService
{
    private static Border? _activeToast;
    private static Grid? _activeHost;
    private static int _sequence;

    public static void Success(string message) => Show(message, true);
    public static void Error(string message) => Show(message, false);

    private static void Show(string message, bool success)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var page = Shell.Current?.CurrentPage ?? Application.Current?.MainPage;
            if (page is null) return;

            Grid host;
            // Only ContentPage exposes Content; Shell pages may be other Page types.
            if (page is not ContentPage contentPage) return;

            if (contentPage.Content is Grid existing)
            {
                host = existing;
            }
            else
            {
                var oldContent = contentPage.Content;
                host = new Grid();
                contentPage.Content = host;
                if (oldContent is not null) host.Children.Add(oldContent);
            }

            if (_activeToast is not null && _activeHost is not null)
                _activeHost.Children.Remove(_activeToast);

            var id = Interlocked.Increment(ref _sequence);
            var toast = new Border
            {
                BackgroundColor = Color.FromArgb(success ? "#176B4D" : "#B42318"),
                Stroke = Colors.Transparent,
                StrokeThickness = 0,
                Padding = new Thickness(16, 12),
                Margin = new Thickness(16, 16, 16, 0),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                MaximumWidthRequest = 380,
                ZIndex = 1000,
                Content = new Label
                {
                    Text = message,
                    TextColor = Colors.White,
                    FontSize = 13,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            };
            _activeToast = toast;
            _activeHost = host;
            host.Children.Add(toast);

            await Task.Delay(TimeSpan.FromSeconds(AppConfig.Application.ToastDurationSeconds));
            if (id == _sequence)
            {
                host.Children.Remove(toast);
                _activeToast = null;
                _activeHost = null;
            }
        });
    }
}
