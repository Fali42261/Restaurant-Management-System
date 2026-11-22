using Microsoft.Maui.Layouts;
using Microsoft.Maui.Controls.Shapes;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Services;

namespace RestaurantManagement.UI.Views;

public sealed class CreateRestaurantPage : ContentPage
{
    private readonly User _user;
    private readonly AuthService _auth = new();
    private readonly Entry _name = new() { Placeholder = "Restaurant name", BackgroundColor = Colors.White };
    private readonly Entry _mobile = new() { Placeholder = "Mobile number", Keyboard = Keyboard.Telephone, BackgroundColor = Colors.White };
    private readonly Editor _address = new() { Placeholder = "Full restaurant address", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 76, BackgroundColor = Colors.White };
    private readonly TimePicker _opening = new() { Time = new TimeSpan(9, 0, 0), BackgroundColor = Colors.White };
    private readonly TimePicker _closing = new() { Time = new TimeSpan(22, 0, 0), BackgroundColor = Colors.White };
    private readonly Dictionary<string, CheckBox> _days = new(StringComparer.OrdinalIgnoreCase);
    private readonly Button _save = new() { Text = "Create Restaurant", BackgroundColor = Color.FromArgb("#2563EB"), TextColor = Colors.White, CornerRadius = 10, HeightRequest = 48, FontAttributes = FontAttributes.Bold };

    public CreateRestaurantPage(User user)
    {
        _user = user;
        Title = "Create Restaurant";
        BackgroundColor = Color.FromArgb("#F4F7FF");
        var days = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row, JustifyContent = FlexJustify.SpaceBetween };
        foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
        {
            var check = new CheckBox { IsChecked = true, Color = Color.FromArgb("#2563EB") };
            _days[day] = check;
            days.Children.Add(new HorizontalStackLayout
            {
                Spacing = 1,
                Children = { check, new Label { Text = day[..3], FontSize = 12, VerticalTextAlignment = TextAlignment.Center, TextColor = Color.FromArgb("#1E3A8A") } }
            });
        }

        var card = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#DCE5F5"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = 20,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label { Text = "Create your restaurant", FontSize = 23, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#172554") },
                        new Label { Text = "Add your restaurant details to start using swapDigit.", FontSize = 12, TextColor = Color.FromArgb("#64748B") },
                        Field("Restaurant Name"), _name,
                        Field("Mobile Number"), _mobile,
                        Field("Address"), _address,
                        Field("Opening Time"), _opening,
                        Field("Closing Time"), _closing,
                        Field("Operating Days"), days,
                        _save
                    }
                }
            }
        };
        Content = new Grid
        {
            Padding = 18,
            Children = { new ScrollView { Content = card, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill } }
        };
        _save.Clicked += SaveClicked;
    }

    private static Label Field(string title) => new() { Text = title, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1E3A8A") };

    private async void SaveClicked(object? sender, EventArgs e)
    {
        if (_save.IsEnabled == false) return;
        var name = _name.Text?.Trim() ?? "";
        var mobile = _mobile.Text?.Trim() ?? "";
        var normalizedMobile = mobile.StartsWith("+", StringComparison.Ordinal) ? mobile[1..] : mobile;
        var address = _address.Text?.Trim() ?? "";
        var days = _days.Where(x => x.Value.IsChecked).Select(x => x.Key).ToArray();
        if (name.Length is < 2 or > 160 || normalizedMobile.Length is < 10 or > 15 ||
            !normalizedMobile.All(char.IsDigit) || address.Length is 0 or > 500 || days.Length == 0 ||
            _opening.Time == _closing.Time)
        {
            await DisplayAlert("Check details", "Enter a valid restaurant name, mobile number, address, different opening/closing times and at least one operating day.", "OK");
            return;
        }

        try
        {
            _save.IsEnabled = false;
            _save.Text = "Creating...";
            var restaurant = await _auth.CreateRestaurantAsync(new CreateRestaurantInput(name, mobile, address, _opening.Time ?? TimeSpan.Zero, _closing.Time ?? TimeSpan.Zero, days));
            await _auth.SelectRestaurantAsync(restaurant);
            RestaurantSession.Set(_user, [restaurant], restaurant);
            await Shell.Current.Navigation.PopModalAsync();
            await Shell.Current.GoToAsync(nameof(DashboardPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Restaurant creation failed", ex.Message, "OK");
        }
        finally
        {
            _save.IsEnabled = true;
            _save.Text = "Create Restaurant";
        }
    }
}
