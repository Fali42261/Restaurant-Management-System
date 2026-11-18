using RestaurantManagement.UI.Services;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class DashboardPage : ContentPage
{
    private double _lastWidth = -1;
    private bool _changingRestaurant;
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
                ((Border)FindByName("UserMenu")).IsVisible = false;
                ((Border)FindByName("NotificationPopup")).IsVisible = false;
            }
        };
    }

    private void ConfigureRestaurantHeader()
    {
        var restaurants = RestaurantSession.Restaurants;
        var selected = RestaurantSession.SelectedRestaurant ?? restaurants.FirstOrDefault();
        _changingRestaurant = true;
        RestaurantSingleName.Text = selected?.Name ?? "Restaurant";
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
            // Reset the view model so stale values from the previous restaurant are not retained.
            var refreshed = new DashboardViewModel();
            BindingContext = refreshed;
            AttachViewModel(refreshed);
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
        var item = await DisplayPromptAsync("Add order", "Enter item name(s):", "Continue", "Cancel", "e.g. Paneer Tikka, Roti");
        if (string.IsNullOrWhiteSpace(item)) return;
        var table = await DisplayPromptAsync("Add order", "Enter table / order type:", "Continue", "Cancel", "e.g. Table 03 / Takeaway");
        if (string.IsNullOrWhiteSpace(table)) return;
        var qtyText = await DisplayPromptAsync("Add order", "Enter total quantity:", "Save", "Cancel", "1", keyboard: Keyboard.Numeric);
        if (!int.TryParse(qtyText, out var quantity) || quantity < 1)
        {
            await DisplayAlert("Invalid quantity", "Please enter a quantity greater than zero.", "OK");
            return;
        }
        vm.AddOrder(item.Trim(), table.Trim(), quantity);
    }

    private async void ViewOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: OrderRecord order }) return;
        await DisplayAlert(order.OrderNumber, $"Items: {order.ItemSummary}\nTable: {order.TableName}\nQuantity: {order.Quantity}\nStatus: {order.Status}\nCreated: {order.CreatedText}", "Close");
    }

    private async void EditOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: OrderRecord order } || BindingContext is not DashboardViewModel vm) return;
        var item = await DisplayPromptAsync("Edit order", "Update item name(s):", "Continue", "Cancel", initialValue: order.ItemSummary);
        if (string.IsNullOrWhiteSpace(item)) return;
        var table = await DisplayPromptAsync("Edit order", "Update table / order type:", "Continue", "Cancel", initialValue: order.TableName);
        if (string.IsNullOrWhiteSpace(table)) return;
        var qtyText = await DisplayPromptAsync("Edit order", "Update quantity:", "Save", "Cancel", initialValue: order.Quantity.ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(qtyText, out var quantity) || quantity < 1)
        {
            await DisplayAlert("Invalid quantity", "Please enter a quantity greater than zero.", "OK");
            return;
        }
        vm.UpdateOrder(order, item.Trim(), table.Trim(), quantity);
    }

    private async void DeleteOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: OrderRecord order } || BindingContext is not DashboardViewModel vm) return;
        var confirm = await DisplayAlert("Delete order", $"Delete {order.OrderNumber}?", "Delete", "Cancel");
        if (confirm) vm.DeleteOrder(order);
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
    private void SettingsMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Settings"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private void SubscriptionMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Subscription"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private async void SubscriptionPlanClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string plan)
            await DisplayAlert("Plan selected", $"{plan}\n\nOnline payment and subscription activation will be enabled after payment gateway integration.", "OK");
    }
    private void HelpMenuClicked(object sender, EventArgs e) { ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Help Support"); ((Border)FindByName("UserMenu")).IsVisible = false; }
    private void LogoutMenuClicked(object sender, EventArgs e) { RestaurantSession.Clear(); ((DashboardViewModel)BindingContext).LogoutCommand.Execute(null); ((Border)FindByName("UserMenu")).IsVisible = false; }

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


    private void PosTotalsChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        decimal.TryParse(PosDiscountEntry.Text, out var discount);
        decimal.TryParse(PosTaxEntry.Text, out var tax);
        vm.PosDiscountPercent = Math.Clamp(discount, 0m, 100m);
        vm.PosTaxPercent = Math.Clamp(tax, 0m, 100m);
        PosDiscountAmountLabel.Text = vm.PosDiscountAmountText;
        PosTaxAmountLabel.Text = vm.PosTaxAmountText;
        PosGrandTotalLabel.Text = vm.PosGrandTotalText;
    }

    private async void PosCheckoutClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DashboardViewModel vm) return;
        decimal.TryParse(PosDiscountEntry.Text, out var discount);
        decimal.TryParse(PosTaxEntry.Text, out var tax);
        decimal.TryParse(PosCashReceivedEntry.Text, out var cashReceived);
        var paymentMethod = PosPaymentPicker.SelectedItem?.ToString() ?? "Cash";
        var success = vm.CompletePosSale(
            PosCustomerEntry.Text ?? "Walk-in Guest",
            PosTableEntry.Text ?? "Takeaway",
            discount, tax, paymentMethod, cashReceived, out var message);
        await DisplayAlert(success ? "Payment completed" : "Cannot complete checkout", message, "OK");
        if (success)
        {
            PosCashReceivedEntry.Text = "";
            PosDiscountEntry.Text = "0";
            PosTaxEntry.Text = "5";
            vm.RefreshPosTotals();
            PosDiscountAmountLabel.Text = vm.PosDiscountAmountText;
            PosTaxAmountLabel.Text = vm.PosTaxAmountText;
            PosGrandTotalLabel.Text = vm.PosGrandTotalText;
            ((DashboardViewModel)BindingContext).NavigateCommand.Execute("Sales / Bills");
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
        vm.AddBill(customer.Trim(), table.Trim(), amount);
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
        UpdateButtons(((Grid)FindByName("Sidebar")), selected);
        UpdateButtons(((ScrollView)FindByName("MobileNavigation")), selected);
    }

    private static void UpdateButtons(Element root, string selected)
    {
        if (root is Button button && button.CommandParameter is string section)
        {
            var active = section.Equals(selected, StringComparison.OrdinalIgnoreCase);
            button.BackgroundColor = Color.FromArgb(active ? "#E4F4F2" : "#173C43");
            button.TextColor = Color.FromArgb(active ? "#123F45" : "#DCE9E8");
            button.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            button.CornerRadius = 9;
        }
        if (root is IElementController controller)
            foreach (var child in controller.LogicalChildren)
                if (child is Element element) UpdateButtons(element, selected);
    }
}
