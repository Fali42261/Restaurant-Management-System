using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.Services.Api;
using RestaurantManagement.UI.Configuration;
using System.Text.Json;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class DashboardPage : ContentPage
{
    private double _lastWidth = -1;
    private bool _changingRestaurant;
    private readonly ApiClient _apiClient = new();
    private string _selectedBillingCycle = "Monthly";
    private readonly Dictionary<string, Guid> _productIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _orderIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _billIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TableDto> _tables = [];
    private readonly IDispatcherTimer _tableTimer;

    public DashboardPage()
    {
        InitializeComponent();
        var vm = new DashboardViewModel();
        BindingContext = vm;
        AttachViewModel(vm);
        Loaded += (_, _) =>
        {
            UpdateNavigationSelection(vm.SelectedSection);
            ConfigureRestaurantHeader();
            ConfigureResponsiveLayout(Width);
            TrialAccessService.StartTrialIfNeeded();
            UpdateTrialLock(vm);
            vm.RefreshTableTimers();
            _ = LoadProductsAsync(vm);
            _ = LoadOrdersAsync(vm);
            _ = LoadBillsAsync(vm);
            // Subscription endpoints are intentionally called only when the user
            // opens Subscription from the menu (or presses its refresh button).
            _tableTimer.Start();
        };
        _tableTimer = Dispatcher.CreateTimer();
        _tableTimer.Interval = TimeSpan.FromSeconds(30);
        _tableTimer.Tick += (_, _) => { if (BindingContext is DashboardViewModel current) current.RefreshTableTimers(); };
        Unloaded += (_, _) => _tableTimer.Stop();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _lastWidth) < 1) return;
        _lastWidth = width;
        ConfigureResponsiveLayout(width);
    }

    private void ConfigureResponsiveLayout(double width)
    {
        var compact = width < 850;
        ((Grid)FindByName("Sidebar")).IsVisible = !compact;
        ((ScrollView)FindByName("MobileNavigation")).IsVisible = compact;
        ((Grid)FindByName("RootLayout")).ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(190);
        ((Label)FindByName("WelcomeLabel")).IsVisible = width >= 850;
        ((Border)FindByName("UserMenu")).IsVisible = false;

        ((Grid)FindByName("StatsGrid")).ColumnDefinitions.Clear();
        var statsColumns = width < 650 ? 2 : 4;
        for (var i = 0; i < statsColumns; i++)
            ((Grid)FindByName("StatsGrid")).ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < ((Grid)FindByName("StatsGrid")).Children.Count; i++)
        {
            Grid.SetColumn((BindableObject)((Grid)FindByName("StatsGrid")).Children[i], i % statsColumns);
            Grid.SetRow((BindableObject)((Grid)FindByName("StatsGrid")).Children[i], i / statsColumns);
        }

        ConfigureResponsiveGrid(((Grid)FindByName("AnalyticsGrid")), width < 720, 1.2);
        ConfigureResponsiveGrid(((Grid)FindByName("BottomGrid")), width < 720, 1.25);

        var contentWidth = width < 850 ? width - 48 : width - 238;
        var cardWidth = width < 650 ? Math.Max(220, contentWidth) :
            width < 1050 ? Math.Max(220, (contentWidth - 16) / 2) :
            Math.Max(220, (contentWidth - 32) / 3);
        foreach (var child in ((FlexLayout)FindByName("TableCardsFlex")).Children)
            if (child is Border card) card.WidthRequest = cardWidth;
    }

    private static void ConfigureResponsiveGrid(Grid grid, bool stacked, double weight)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (stacked)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < grid.Children.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetColumn((BindableObject)grid.Children[i], 0);
                Grid.SetRow((BindableObject)grid.Children[i], i);
            }
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(weight, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < grid.Children.Count; i++)
            {
                Grid.SetColumn((BindableObject)grid.Children[i], i);
                Grid.SetRow((BindableObject)grid.Children[i], 0);
            }
        }
    }

    private void AttachViewModel(DashboardViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.SelectedSection))
            {
                UpdateTrialLock(vm);
                UpdateNavigationSelection(vm.SelectedSection);
                if (vm.SelectedSection.Equals("Orders", StringComparison.OrdinalIgnoreCase))
                    _ = LoadOrdersAsync(vm);
                else if (vm.SelectedSection.Equals("POS Billing", StringComparison.OrdinalIgnoreCase))
                    _ = LoadProductsAsync(vm);
                else if (vm.SelectedSection.Equals("Sales / Bills", StringComparison.OrdinalIgnoreCase))
                    _ = LoadBillsAsync(vm);
                else if (vm.SelectedSection.Equals("Subscription", StringComparison.OrdinalIgnoreCase))
                    _ = LoadSubscriptionDataAsync();
                ((Border)FindByName("UserMenu")).IsVisible = false;
                ((Border)FindByName("NotificationPopup")).IsVisible = false;
            }
        };
    }

    private void ConfigureRestaurantHeader()
    {
        var restaurants = RestaurantSession.Restaurants;
        var selected = RestaurantSession.SelectedRestaurant ?? restaurants.FirstOrDefault();
        ProfileName.Text = RestaurantSession.User?.FullName ?? "User";
        ProfileRole.Text = selected?.Role ?? RestaurantSession.User?.Role ?? "Staff";
        _changingRestaurant = true;
        RestaurantSingleName.Text = selected?.Name ?? "Restaurant";
        SidebarRestaurantName.Text = selected?.Name ?? "My Restaurant";
        RestaurantSingleName.IsVisible = restaurants.Count == 1;
        RestaurantPicker.IsVisible = restaurants.Count > 1;
        RestaurantPicker.ItemsSource = restaurants;
        RestaurantPicker.ItemDisplayBinding = new Binding(nameof(RestaurantInfo.Name));
        RestaurantPicker.SelectedItem = selected;
        _changingRestaurant = false;
    }

    private async void RestaurantPickerChanged(object sender, EventArgs e)
    {
        if (_changingRestaurant || RestaurantPicker.SelectedItem is not RestaurantInfo selected ||
            selected.RestaurantId == RestaurantSession.SelectedRestaurant?.RestaurantId)
            return;

        try
        {
            RestaurantPicker.IsEnabled = false;
            await new AuthService().SelectRestaurantAsync(selected);
            RestaurantSession.Select(selected);
            RestaurantSingleName.Text = selected.Name;
            SidebarRestaurantName.Text = selected.Name;
            // Reset the view model so stale values from the previous restaurant are not retained.
            var refreshed = new DashboardViewModel();
            BindingContext = refreshed;
            AttachViewModel(refreshed);
            _productIds.Clear();
            _orderIds.Clear();
            _billIds.Clear();
            _tables.Clear();
            _ = LoadProductsAsync(refreshed);
            _ = LoadOrdersAsync(refreshed);
            _ = LoadBillsAsync(refreshed);
            UpdateNavigationSelection(refreshed.SelectedSection);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Restaurant switch failed", ex.Message, "OK");
            _changingRestaurant = true;
            RestaurantPicker.SelectedItem = RestaurantSession.SelectedRestaurant;
            _changingRestaurant = false;
        }
        finally
        {
            RestaurantPicker.IsEnabled = true;
        }
    }

    private void TableActionClicked(object sender, EventArgs e)
    {
        if (sender is Button { BindingContext: TableRecord table } && BindingContext is DashboardViewModel vm)
            vm.AdvanceTable(table);
    }

    private async void AddOrderClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        await LoadProductsAsync(vm);
        if (vm.SelfServiceProducts.Count == 0)
        {
            await DisplayAlert("Menu unavailable", "Products could not be loaded from the restaurant API.", "OK");
            return;
        }
        var names = vm.SelfServiceProducts.Select(x => x.Name).ToArray();
        var selected = await DisplayActionSheet("Select order item", "Cancel", null, names);
        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel") return;
        var quantityText = await DisplayPromptAsync("Add order", "Quantity:", "Continue", "Cancel", "1", keyboard: Keyboard.Numeric);
        if (!int.TryParse(quantityText, out var quantity) || quantity < 1 || !_productIds.TryGetValue(selected, out var productId))
        {
            await DisplayAlert("Invalid order", "Select a valid product and quantity.", "OK");
            return;
        }
        var service = new OrdersPosApiService();
        try
        {
            var result = await service.CheckoutAsync(new PosCheckoutRequest(
                "Walk-in Guest", "Takeaway", null, 0m, 0m, "Unpaid", 0m,
                [new PosCheckoutItem(productId, quantity, null)]));
            await LoadOrdersAsync(vm);
            await DisplayAlert("Order saved", $"Order {result?.OrderNumber} was saved and sent to KOT.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Order not saved", ex.Message, "OK");
        }
    }

    private async void ViewOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: OrderRecord order }) return;
        await DisplayAlert(order.OrderNumber, $"Items: {order.ItemSummary}\nTable: {order.TableName}\nQuantity: {order.Quantity}\nStatus: {order.Status}\nCreated: {order.CreatedText}", "Close");
    }

    private async void EditOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: OrderRecord order } || BindingContext is not DashboardViewModel vm) return;
        if (!_orderIds.TryGetValue(order.OrderNumber, out var orderId))
        {
            await DisplayAlert("Order unavailable", "Refresh the Orders list and try again.", "OK");
            return;
        }
        await LoadProductsAsync(vm);
        var item = await DisplayPromptAsync("Edit order", "Enter product name(s), separated by commas:", "Continue", "Cancel", initialValue: string.Join(", ", order.ItemSummary.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(" × ", StringSplitOptions.None)[0])));
        if (string.IsNullOrWhiteSpace(item)) return;
        var qtyText = await DisplayPromptAsync("Edit order", "Total quantity:", "Save", "Cancel", initialValue: Math.Max(1, order.Quantity).ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(qtyText, out var quantity) || quantity < 1)
        {
            await DisplayAlert("Invalid quantity", "Please enter a quantity greater than zero.", "OK");
            return;
        }
        var selectedProducts = item.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (selectedProducts.Length == 0 || selectedProducts.Any(x => !_productIds.ContainsKey(x)))
        {
            await DisplayAlert("Product not found", "Use product names from the restaurant menu, separated by commas.", "OK");
            return;
        }
        var lines = selectedProducts.Select((name, index) => new ReplaceOrderItem(
            _productIds[name], index == 0 ? quantity - selectedProducts.Length + 1 : 1, null)).ToList();
        if (lines.Any(x => x.Quantity < 1))
        {
            await DisplayAlert("Invalid quantity", "Total quantity must be at least the number of selected products.", "OK");
            return;
        }
        try
        {
            await new OrdersPosApiService().ReplaceOrderItemsAsync(orderId, lines);
            await LoadOrdersAsync(vm);
            await LoadBillsAsync(vm);
            await DisplayAlert("Order updated", "Order items and unpaid invoice totals were updated.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Order not updated", ex.Message, "OK");
        }
    }

    private async void DeleteOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: OrderRecord order } || BindingContext is not DashboardViewModel vm) return;
        var confirm = await DisplayAlert("Delete order", $"Delete {order.OrderNumber}?", "Delete", "Cancel");
        if (!confirm) return;
        if (!_orderIds.TryGetValue(order.OrderNumber, out var orderId))
        {
            await DisplayAlert("Order unavailable", "Refresh the Orders list and try again.", "OK");
            return;
        }
        try
        {
            await new OrdersPosApiService().DeleteOrderAsync(orderId);
            await LoadOrdersAsync(vm);
        }
        catch (Exception ex) { await DisplayAlert("Order not deleted", ex.Message, "OK"); }
    }

    private FloorRecord? _editingFloor;
    private TableRecord? _editingTable;
    private bool _editingFloorMode;

    private void UpdateTrialLock(DashboardViewModel vm)
    {
        var expired = !TrialAccessService.HasAccess;
        ((Grid)FindByName("TrialLockOverlay")).IsVisible = expired && !vm.IsSubscriptionSection;
    }

    private void OpenSubscriptionFromTrial(object sender, EventArgs e)
    {
        ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Subscription");
        UpdateTrialLock((DashboardViewModel)BindingContext);
    }

    private void DarkThemeClicked(object sender, EventArgs e)
    {
        ThemeService.SetTheme(AppTheme.Dark);
        ((Border)FindByName("UserMenu")).IsVisible = false;
    }

    private void SystemThemeClicked(object sender, EventArgs e)
    {
        ThemeService.SetTheme(AppTheme.Unspecified);
        ((Border)FindByName("UserMenu")).IsVisible = false;
    }

    private void ToggleUserMenu(object sender, TappedEventArgs e)
    {
        var menu = (Border)FindByName("UserMenu");
        menu.IsVisible = !menu.IsVisible;
        ((Border)FindByName("NotificationPopup")).IsVisible = false;
    }

    private void ToggleNotificationPopup(object sender, EventArgs e)
    {
        var popup = (Border)FindByName("NotificationPopup");
        popup.IsVisible = !popup.IsVisible;
        ((Border)FindByName("UserMenu")).IsVisible = false;
    }

    private void CloseNotificationPopup(object sender, EventArgs e) => ((Border)FindByName("NotificationPopup")).IsVisible = false;
    private void MenuManagementMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Menu Management"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private void MenuManagementMenuTapped(object sender, TappedEventArgs e) => MenuManagementMenuClicked(sender, EventArgs.Empty);
    private void SettingsMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Settings"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private void SubscriptionMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Subscription"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private void SettingsMenuTapped(object sender, TappedEventArgs e) => SettingsMenuClicked(sender, EventArgs.Empty);
    private void SubscriptionMenuTapped(object sender, TappedEventArgs e) => SubscriptionMenuClicked(sender, EventArgs.Empty);
    private void LogoutMenuTapped(object sender, TappedEventArgs e) => LogoutMenuClicked(sender, EventArgs.Empty);
    private async void SubscriptionPlanClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string planCode)
            return;

        try
        {
            var response = await _apiClient.PostResponseAsync<JsonElement>(
                AppConfig.Routes.SelectSubscription,
                new { planCode, billingCycle = _selectedBillingCycle },
                authorized: true);
            await DisplayAlert("Subscription activated",
                response.Message ?? "Your subscription has been saved successfully.", "OK");
            await LoadSubscriptionDataAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Subscription not saved", ex.Message, "OK");
        }
    }

    private async void RefreshSubscriptionClicked(object sender, EventArgs e)
    {
        if (RefreshSubscriptionButton is null || !RefreshSubscriptionButton.IsEnabled)
            return;

        RefreshSubscriptionButton.IsEnabled = false;
        RefreshSubscriptionButton.Text = "…";
        try
        {
            await LoadSubscriptionDataAsync();
        }
        finally
        {
            RefreshSubscriptionButton.Text = "⟳";
            RefreshSubscriptionButton.IsEnabled = true;
        }
    }

    private void BillingCycleChanged(object sender, EventArgs e)
    {
        if (BillingCyclePicker.SelectedItem is string cycle)
        {
            _selectedBillingCycle = cycle;
            _ = LoadSubscriptionPlansAsync();
        }
    }

    private async Task LoadSubscriptionDataAsync()
    {
        if (SubscriptionPlansLayout is null || BillingCyclePicker is null)
            return;

        BillingCyclePicker.ItemsSource = new[] { "Monthly", "Quarterly", "Yearly" };
        if (BillingCyclePicker.SelectedIndex < 0)
            BillingCyclePicker.SelectedIndex = 0;

        CurrentSubscriptionLabel.Text = "Loading current subscription...";
        await LoadSubscriptionPlansAsync();

        try
        {
            var current = await _apiClient.GetAsync<SubscriptionCurrentDto>(
                AppConfig.Routes.CurrentSubscription, authorized: true);
            CurrentSubscriptionLabel.Text = current is null
                ? "No active subscription found for this restaurant."
                : $"{current.PlanName ?? current.PlanCode} • {current.Status} • Expires {current.ExpiresAt:dd MMM yyyy}";
        }
        catch (Exception ex)
        {
            CurrentSubscriptionLabel.Text = $"Unable to load subscription: {ex.Message}";
        }
    }

    private async Task LoadSubscriptionPlansAsync()
    {
        try
        {
            var plans = await _apiClient.GetAsync<List<SubscriptionPlanDto>>(
                AppConfig.Routes.SubscriptionPlans);
            SubscriptionPlansLayout.Children.Clear();

            if (plans is null || plans.Count == 0)
            {
                SubscriptionPlansLayout.Children.Add(new Label { Text = "No subscription plans are currently available." });
                return;
            }

            foreach (var plan in plans)
            {
                var price = _selectedBillingCycle switch
                {
                    "Quarterly" => plan.QuarterlyPrice,
                    "Yearly" => plan.YearlyPrice,
                    _ => plan.MonthlyPrice
                };

                var planName = string.IsNullOrWhiteSpace(plan.Name) ? plan.PlanCode : plan.Name;
                var features = ParseSubscriptionFeatures(plan.FeaturesJson);

                var content = new VerticalStackLayout { Spacing = 10 };
                content.Children.Add(new Label
                {
                    Text = planName,
                    FontSize = 19,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#2563EB")
                });
                content.Children.Add(new Label
                {
                    Text = $"₹{price:N0}",
                    FontSize = 29,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#F1F5F9") : Color.FromArgb("#0F1F45")
                });
                content.Children.Add(new Label
                {
                    Text = $"per {_selectedBillingCycle.ToLowerInvariant()}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#718096")
                });
                content.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#34445F") : Color.FromArgb("#E2E8F0")
                });

                foreach (var feature in features)
                {
                    content.Children.Add(new Label
                    {
                        Text = $"✓  {feature}",
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.WordWrap,
                        TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                            ? Color.FromArgb("#DCE6F5") : Color.FromArgb("#334155")
                    });
                }

                var choose = new Button
                {
                    Text = $"Choose {planName}",
                    CommandParameter = plan.PlanCode,
                    BackgroundColor = Color.FromArgb("#2563EB"),
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    HeightRequest = 44
                };
                choose.Clicked += SubscriptionPlanClicked;
                content.Children.Add(choose);

                SubscriptionPlansLayout.Children.Add(new Border
                {
                    Content = content,
                    Padding = 18,
                    Margin = new Thickness(0, 0, 12, 12),
                    WidthRequest = 270,
                    MinimumHeightRequest = 280,
                    Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#34445F") : Color.FromArgb("#DCE5F5"),
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                    BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#111C35") : Colors.White
                });
            }
        }
        catch (Exception ex)
        {
            SubscriptionPlansLayout.Children.Clear();
            SubscriptionPlansLayout.Children.Add(new Label { Text = $"Unable to load plans: {ex.Message}" });
        }
    }

    private static List<string> ParseSubscriptionFeatures(string? featuresJson)
    {
        if (string.IsNullOrWhiteSpace(featuresJson))
            return new List<string> { "Restaurant management features included" };

        try
        {
            var features = JsonSerializer.Deserialize<List<string>>(featuresJson);
            return features?.Where(feature => !string.IsNullOrWhiteSpace(feature)).ToList()
                ?? new List<string> { "Restaurant management features included" };
        }
        catch (JsonException)
        {
            // A malformed optional feature list must never prevent the plan card itself from rendering.
            return new List<string> { "Restaurant management features included" };
        }
    }

    private sealed class SubscriptionPlanDto
    {
        public string PlanCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public decimal QuarterlyPrice { get; set; }
        public decimal YearlyPrice { get; set; }
        public string? FeaturesJson { get; set; }
    }

    private sealed class SubscriptionCurrentDto
    {
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
    private void HelpMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Help Support"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private async void LogoutMenuClicked(object sender, EventArgs e)
    {
        try { await new AuthService().SignOutAsync(); }
        catch { RestaurantSession.Clear(); }
        ((DashboardViewModel)BindingContext).LogoutCommand.Execute(null);
        ((Border)FindByName("UserMenu")).IsVisible = false;
    }

    private void AddFloorClicked(object sender, EventArgs e)
    {
        _editingFloor = null; _editingTable = null; _editingFloorMode = true;
        ManagementPopupTitle.Text = "Add Floor"; FloorFields.IsVisible = true; TableFields.IsVisible = false;
        FloorNameEntry.Text = ""; ManagementSaveButton.Text = "Add Floor"; ManagementPopup.IsVisible = true;
    }

    private void EditFloorClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: FloorRecord floor }) return;
        _editingFloor = floor; _editingTable = null; _editingFloorMode = true;
        ManagementPopupTitle.Text = "Edit Floor"; FloorFields.IsVisible = true; TableFields.IsVisible = false;
        FloorNameEntry.Text = floor.Name; ManagementSaveButton.Text = "Save Changes"; ManagementPopup.IsVisible = true;
    }

    private async void DeleteFloorClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: FloorRecord floor }) return;
        if (await DisplayAlert("Delete floor", $"Delete {floor.Name}?", "Delete", "Cancel"))
        {
            var deleted = ((DashboardViewModel)BindingContext).DeleteFloor(floor);
            if (!deleted)
                await DisplayAlert("Cannot delete floor", "This floor is the only remaining floor or still has tables. Move or delete its tables first.", "OK");
        }
    }

    private void AddTableClicked(object sender, EventArgs e)
    {
        _editingTable = null; _editingFloor = null; _editingFloorMode = false;
        ManagementPopupTitle.Text = "Add Table"; FloorFields.IsVisible = false; TableFields.IsVisible = true;
        TableNameEntry.Text = ""; TableSeatsEntry.Text = "4";
        TableFloorPicker.ItemsSource = ((DashboardViewModel)BindingContext).Floors.Select(f => f.Name).ToList();
        TableFloorPicker.SelectedIndex = 0; ManagementSaveButton.Text = "Add Table"; ManagementPopup.IsVisible = true;
    }

    private void EditTableClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TableRecord table }) return;
        _editingTable = table; _editingFloor = null; _editingFloorMode = false;
        ManagementPopupTitle.Text = "Edit Table"; FloorFields.IsVisible = false; TableFields.IsVisible = true;
        TableNameEntry.Text = table.Name; TableSeatsEntry.Text = table.Seats.ToString();
        TableFloorPicker.ItemsSource = ((DashboardViewModel)BindingContext).Floors.Select(f => f.Name).ToList();
        TableFloorPicker.SelectedItem = table.FloorName; ManagementSaveButton.Text = "Save Changes"; ManagementPopup.IsVisible = true;
    }

    private async void DeleteTableClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TableRecord table }) return;
        if (await DisplayAlert("Delete table", $"Delete {table.Name}?", "Delete", "Cancel"))
            ((DashboardViewModel)BindingContext).DeleteTable(table);
    }

    private void CancelManagementPopup(object sender, EventArgs e) => ManagementPopup.IsVisible = false;

    private async void SaveManagementPopup(object sender, EventArgs e)
    {
        var vm = (DashboardViewModel)BindingContext;
        if (_editingFloorMode)
        {
            var name = FloorNameEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) { await DisplayAlert("Required", "Enter a floor name.", "OK"); return; }
            var saved = _editingFloor is null ? vm.AddFloor(name) : vm.UpdateFloor(_editingFloor, name);
            if (!saved)
            {
                await DisplayAlert("Floor not saved", "A floor with this name may already exist. Enter a unique floor name.", "OK");
                return;
            }
        }
        else
        {
            var name = TableNameEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name) || !int.TryParse(TableSeatsEntry.Text, out var seats) || seats < 1 || TableFloorPicker.SelectedItem is not string floor)
            { await DisplayAlert("Check details", "Enter a table name, valid seat count and floor.", "OK"); return; }
            var saved = _editingTable is null ? vm.AddTable(name, seats, floor) : vm.UpdateTable(_editingTable, name, seats, floor);
            if (!saved)
            {
                await DisplayAlert("Table not saved", "A table with this name may already exist, or the selected floor is no longer available. Check the details and try again.", "OK");
                return;
            }
        }
        ManagementPopup.IsVisible = false;
    }

    private async void AddReservationClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        var guest = ReservationGuestEntry.Text?.Trim() ?? "";
        var phone = ReservationPhoneEntry.Text?.Trim() ?? "";
        var table = ReservationTableEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(guest) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(table) ||
            !int.TryParse(ReservationGuestsEntry.Text, out var guests) || guests < 1)
        {
            await DisplayAlert("Check booking details", "Enter guest name, mobile number, table and a valid guest count.", "OK");
            return;
        }

        vm.AddReservation(guest, phone, ReservationDatePicker.Date ?? DateTime.Today, ReservationTimePicker.Time ?? new TimeSpan(19, 0, 0), table, guests);
        ReservationGuestEntry.Text = "";
        ReservationPhoneEntry.Text = "";
        ReservationTableEntry.Text = "";
        ReservationGuestsEntry.Text = "";
        await DisplayAlert("Reservation added", "The booking has been added with Pending status.", "OK");
    }


    private async Task LoadProductsAsync(DashboardViewModel vm)
    {
        _productIds.Clear();
        vm.SelfServiceProducts.Clear();
        vm.SetMenuLoadStatus("Refreshing menu items...");
        try
        {
            var page = await new OrdersPosApiService().GetProductsAsync();
            var products = page?.Items ?? [];
            var menuItems = new List<MenuProduct>();

            foreach (var product in products)
            {
                var category = string.IsNullOrWhiteSpace(product.CategoryName)
                    ? (string.IsNullOrWhiteSpace(product.Category) ? "Uncategorised" : product.Category)
                    : product.CategoryName;

                menuItems.Add(new MenuProduct(
                    product.Name, category, product.Price,
                    product.Description ?? "No description available.",
                    product.IsAvailable));

                if (product.IsAvailable)
                {
                    _productIds[product.Name] = product.ProductId;
                    vm.SelfServiceProducts.Add(new MenuProduct(
                        product.Name, category, product.Price, product.Description ?? ""));
                }
            }

            vm.SetMenuProducts(menuItems);
            vm.SetMenuLoadStatus(products.Count == 0
                ? "No menu items found. Items added to your restaurant menu will appear here."
                : $"Showing {products.Count} menu item(s) from your restaurant.");
        }
        catch (Exception ex)
        {
            vm.SetMenuLoadStatus($"Unable to load menu items: {ex.Message}");
        }
    }

    // Menu Management is currently a visual dummy-data preview. CRUD is intentionally not persisted.
    private async void AddDummyCategoryClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Category preview", "Category creation will be enabled after the dummy UI is approved.", "OK");
    }

    private async void EditDummyCategoryClicked(object sender, EventArgs e)
    {
        var category = (sender as Button)?.CommandParameter?.ToString() ?? "Category";
        await DisplayAlert("Edit category", $"'{category}' is shown as a UI preview. No changes are saved.", "OK");
    }

    private async void DeleteDummyCategoryClicked(object sender, EventArgs e)
    {
        var category = (sender as Button)?.CommandParameter?.ToString() ?? "Category";
        await DisplayAlert("Delete category", $"'{category}' is demo content only. No category was deleted.", "OK");
    }

    private async void AddDummyMenuItemClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Add menu item", "The add-item screen will be implemented after this UI preview is approved.", "OK");
    }

    private async void EditDummyMenuItemClicked(object sender, EventArgs e)
    {
        var item = (sender as Button)?.CommandParameter?.ToString() ?? "Menu item";
        await DisplayAlert("Edit menu item", $"'{item}' is demo content only. The edit form will be added in the next phase.", "OK");
    }

    private async void DeleteDummyMenuItemClicked(object sender, EventArgs e)
    {
        var item = (sender as Button)?.CommandParameter?.ToString() ?? "Menu item";
        await DisplayAlert("Delete menu item", $"'{item}' is demo content only. No item was deleted.", "OK");
    }

    private async void RefreshMenuClicked(object sender, EventArgs e)
    {
        if (BindingContext is DashboardViewModel vm)
            await LoadProductsAsync(vm);
    }

    private void MenuSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is DashboardViewModel vm)
            vm.MenuSearchText = e.NewTextValue ?? "";
    }

    private void MenuCategoryChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && BindingContext is DashboardViewModel vm &&
            picker.SelectedItem is string category)
            vm.SelectedMenuCategory = category;
    }

    private async Task LoadOrdersAsync(DashboardViewModel vm)
    {
        _orderIds.Clear();
        vm.Orders.Clear();
        try
        {
            var service = new OrdersPosApiService();
            var page = await service.GetOrdersAsync();
            var tableRows = await service.GetTablesAsync();
            _tables.Clear();
            if (tableRows is not null) _tables.AddRange(tableRows);
            foreach (var row in page?.Items ?? [])
            {
                _orderIds[row.OrderNumber] = row.OrderId;
                var tableName = row.TableId.HasValue
                    ? _tables.FirstOrDefault(t => t.TableId == row.TableId.Value)?.Name ?? row.OrderType
                    : row.OrderType;
                vm.Orders.Add(new OrderRecord(row.OrderNumber,
                    string.IsNullOrWhiteSpace(row.ItemSummary) ? (row.CustomerName ?? row.OrderType) : row.ItemSummary,
                    tableName, Math.Max(1, row.Quantity), row.Status, row.CreatedAt));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Orders API unavailable", ex.Message, "OK");
        }
    }

    private async Task LoadBillsAsync(DashboardViewModel vm)
    {
        _billIds.Clear();
        vm.Bills.Clear();
        try
        {
            var page = await new OrdersPosApiService().GetBillsAsync();
            foreach (var row in page?.Items ?? [])
            {
                _billIds[row.InvoiceNumber] = row.BillId;
                vm.Bills.Add(new BillRecord(row.InvoiceNumber, row.CustomerName, row.OrderType,
                    row.Total, row.Status, row.CreatedAt, row.PaymentMethod, row.Subtotal,
                    row.Discount, row.Tax, row.AmountPaid, Math.Max(0m, row.AmountPaid - row.Total), row.ItemSummary));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Bills API unavailable", ex.Message, "OK");
        }
    }

    private void PosTotalsChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        decimal.TryParse(((Entry)FindByName("PosDiscountEntry")).Text, out var discount);
        decimal.TryParse(((Entry)FindByName("PosTaxEntry")).Text, out var tax);
        vm.PosDiscountPercent = Math.Clamp(discount, 0m, 100m);
        vm.PosTaxPercent = Math.Clamp(tax, 0m, 100m);
        ((Label)FindByName("PosDiscountAmountLabel")).Text = vm.PosDiscountAmountText;
        ((Label)FindByName("PosTaxAmountLabel")).Text = vm.PosTaxAmountText;
        ((Label)FindByName("PosGrandTotalLabel")).Text = vm.PosGrandTotalText;
    }

    private async void PosCheckoutClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        decimal.TryParse(((Entry)FindByName("PosDiscountEntry")).Text, out var discount);
        decimal.TryParse(((Entry)FindByName("PosTaxEntry")).Text, out var tax);
        decimal.TryParse(((Entry)FindByName("PosCashReceivedEntry")).Text, out var cashReceived);
        var paymentMethod = ((Picker)FindByName("PosPaymentPicker")).SelectedItem?.ToString() ?? "Cash";
        if (vm.PosCart.Count == 0)
        {
            await DisplayAlert("Cannot checkout", "Add at least one product to the bill.", "OK");
            return;
        }
        await LoadProductsAsync(vm);
        var missing = vm.PosCart.Where(x => !_productIds.ContainsKey(x.ProductName)).Select(x => x.ProductName).ToArray();
        if (missing.Length > 0)
        {
            await DisplayAlert("Menu changed", $"These products are no longer available: {string.Join(", ", missing)}. Refresh the menu and try again.", "OK");
            return;
        }
        var orderText = ((Entry)FindByName("PosTableEntry")).Text?.Trim() ?? "Takeaway";
        var selectedTable = _tables.FirstOrDefault(t =>
            t.Name.Equals(orderText, StringComparison.OrdinalIgnoreCase) ||
            $"Table {t.Name}".Equals(orderText, StringComparison.OrdinalIgnoreCase));
        var orderType = selectedTable is not null ? "DineIn" :
            orderText.Equals("Delivery", StringComparison.OrdinalIgnoreCase) ? "Delivery" : "Takeaway";
        if (orderType == "DineIn" && selectedTable is null)
        {
            await DisplayAlert("Table not found", "Enter a table name that exists in this restaurant, or choose Takeaway / Delivery.", "OK");
            return;
        }
        var method = paymentMethod.Equals("Other", StringComparison.OrdinalIgnoreCase) ? "Online" : paymentMethod;
        var subtotal = vm.PosCart.Sum(x => x.LineTotal);
        var discountAmount = Math.Round(subtotal * Math.Clamp(discount, 0m, 100m) / 100m, 2);
        var taxAmount = Math.Round((subtotal - discountAmount) * Math.Clamp(tax, 0m, 100m) / 100m, 2);
        var payable = subtotal - discountAmount + taxAmount;
        if (method.Equals("Cash", StringComparison.OrdinalIgnoreCase) && cashReceived < payable)
        {
            await DisplayAlert("Insufficient cash", $"Cash received must be at least ₹{payable:N2}.", "OK");
            return;
        }
        if (!method.Equals("Cash", StringComparison.OrdinalIgnoreCase) &&
            !await DisplayAlert("Confirm payment", $"Confirm that ₹{payable:N2} has been collected through {method}.", "Confirm", "Cancel"))
            return;
        var items = vm.PosCart.Select(x => new PosCheckoutItem(_productIds[x.ProductName], x.Quantity, null)).ToList();
        try
        {
            var result = await new OrdersPosApiService().CheckoutAsync(new PosCheckoutRequest(
                ((Entry)FindByName("PosCustomerEntry")).Text?.Trim(), orderType, selectedTable?.TableId,
                discountAmount, taxAmount, method, cashReceived, items));
            if (result is null) throw new InvalidOperationException("The API did not return the saved invoice.");
            vm.PosCart.Clear();
            vm.RefreshPosTotals();
            await LoadOrdersAsync(vm);
            await LoadBillsAsync(vm);
            ((Entry)FindByName("PosCashReceivedEntry")).Text = "";
            ((Entry)FindByName("PosDiscountEntry")).Text = "0";
            ((Entry)FindByName("PosTaxEntry")).Text = "5";
            ((Label)FindByName("PosDiscountAmountLabel")).Text = vm.PosDiscountAmountText;
            ((Label)FindByName("PosTaxAmountLabel")).Text = vm.PosTaxAmountText;
            ((Label)FindByName("PosGrandTotalLabel")).Text = vm.PosGrandTotalText;
            await DisplayAlert("Payment completed", $"Order {result.OrderNumber}\nInvoice {result.InvoiceNumber}\nTotal ₹{result.Total:N2}\nPaid ₹{result.AmountPaid:N2}\nStatus: {result.Status}", "OK");
            vm.NavigateCommand.Execute("Sales / Bills");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Checkout failed", ex.Message, "OK");
        }
    }

    private async void CreateBillClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        var customer = await DisplayPromptAsync("Create bill", "Customer name:", "Continue", "Cancel", "Walk-in Guest");
        if (string.IsNullOrWhiteSpace(customer)) return;
        var table = await DisplayPromptAsync("Create bill", "Table / order type:", "Continue", "Cancel", "Takeaway");
        if (string.IsNullOrWhiteSpace(table)) return;
        var amountText = await DisplayPromptAsync("Create bill", "Bill amount (₹):", "Create", "Cancel", "0", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
        {
            await DisplayAlert("Invalid amount", "Enter a bill amount greater than zero.", "OK");
            return;
        }
        var normalizedType = table.Trim().Equals("Delivery", StringComparison.OrdinalIgnoreCase) ? "Delivery" :
            table.Trim().StartsWith("Table", StringComparison.OrdinalIgnoreCase) ||
            (table.Trim().StartsWith("T", StringComparison.OrdinalIgnoreCase) && !table.Trim().Equals("Takeaway", StringComparison.OrdinalIgnoreCase)) ? "DineIn" : "Takeaway";
        try
        {
            await new OrdersPosApiService().CreateBillAsync(new CreateBillApiRequest(
                null, customer.Trim(), normalizedType, amount, 0m, 0m, amount, "Unpaid", 0m));
            await LoadBillsAsync(vm);
            await DisplayAlert("Bill created", "The invoice was saved to the restaurant database.", "OK");
        }
        catch (Exception ex) { await DisplayAlert("Bill not created", ex.Message, "OK"); }
    }

    private async void MarkBillPaidClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: BillRecord bill } || BindingContext is not DashboardViewModel vm) return;
        if (bill.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase)) return;
        if (!_billIds.TryGetValue(bill.InvoiceNumber, out var billId))
        {
            await DisplayAlert("Invoice unavailable", "Refresh the bills list and try again.", "OK");
            return;
        }
        try
        {
            await new OrdersPosApiService().MarkBillPaidAsync(billId);
            await LoadBillsAsync(vm);
            await DisplayAlert("Payment updated", $"{bill.InvoiceNumber} is now marked as paid.", "OK");
        }
        catch (Exception ex) { await DisplayAlert("Payment not updated", ex.Message, "OK"); }
    }

    private async void ViewInvoiceClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: BillRecord bill }) return;
        await DisplayAlert(bill.InvoiceNumber,
            $"Customer: {bill.Customer}\\nTable / Type: {bill.Table}\\nItems: {bill.Items}\\nSubtotal: ₹{bill.Subtotal:N2}\\nDiscount: ₹{bill.Discount:N2}\\nTax: ₹{bill.Tax:N2}\\nTotal: {bill.AmountText}\\nPayment: {bill.PaymentMethod}\\nCash received: ₹{bill.CashReceived:N2}\\nChange: ₹{bill.Change:N2}\\nStatus: {bill.Status}\\nCreated: {bill.CreatedText}",
            "Close");
    }

    private void UpdateNavigationSelection(string selected)
    {
        UpdateButtons(((Grid)FindByName("Sidebar")), selected, mobile: false);
        UpdateButtons(((ScrollView)FindByName("MobileNavigation")), selected, mobile: true);
    }

    private static void UpdateButtons(Element root, string selected, bool mobile)
    {
        if (root is Border navItem)
        {
            var tap = navItem.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
            if (tap?.CommandParameter is string navSection)
            {
                var active = navSection.Equals(selected, StringComparison.OrdinalIgnoreCase);
                navItem.BackgroundColor = Color.FromArgb(active ? "#2563EB" : "#142342");
                if (navItem.Content is Grid navGrid)
                {
                    foreach (var child in navGrid.Children)
                    {
                        if (child is Label label)
                        {
                            label.TextColor = Color.FromArgb(active ? "#FFFFFF" : "#DCE6FF");
                            if (Grid.GetColumn(label) == 1)
                                label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
                        }
                    }
                }
            }
        }
        if (root is Button button && button.CommandParameter is string section)
        {
            var active = section.Equals(selected, StringComparison.OrdinalIgnoreCase);
            button.BackgroundColor = Color.FromArgb(active ? "#2563EB" : mobile ? "#E8EEFF" : "#142342");
            button.BorderColor = button.BackgroundColor;
            button.BorderWidth = 0;
            button.TextColor = Color.FromArgb(active ? "#FFFFFF" : mobile ? "#1E3A8A" : "#DCE6FF");
            button.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            button.CornerRadius = 9;
        }
        if (root is IElementController controller)
            foreach (var child in controller.LogicalChildren)
                if (child is Element element) UpdateButtons(element, selected, mobile);
    }
}
