using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class DashboardPage : ContentPage
{
    private double _lastWidth = -1;
    private readonly IDispatcherTimer _tableTimer;

    public DashboardPage()
    {
        InitializeComponent();
        var vm = new DashboardViewModel();
        BindingContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.SelectedSection))
                UpdateNavigationSelection(vm.SelectedSection);
        };
        Loaded += (_, _) =>
        {
            UpdateNavigationSelection(vm.SelectedSection);
            ConfigureResponsiveLayout(Width);
            vm.RefreshTableTimers();
            _tableTimer.Start();
        };
        _tableTimer = Dispatcher.CreateTimer();
        _tableTimer.Interval = TimeSpan.FromSeconds(30);
        _tableTimer.Tick += (_, _) => vm.RefreshTableTimers();
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
        Sidebar.IsVisible = !compact;
        MobileNavigation.IsVisible = compact;
        RootLayout.ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(190);
        WelcomeLabel.IsVisible = width >= 850;
        SearchPanel.IsVisible = width >= 1000;
        UserMenu.IsVisible = false;

        StatsGrid.ColumnDefinitions.Clear();
        var statsColumns = width < 650 ? 2 : 4;
        for (var i = 0; i < statsColumns; i++)
            StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < StatsGrid.Children.Count; i++)
        {
            Grid.SetColumn((BindableObject)StatsGrid.Children[i], i % statsColumns);
            Grid.SetRow((BindableObject)StatsGrid.Children[i], i / statsColumns);
        }

        ConfigureResponsiveGrid(AnalyticsGrid, width < 720, 1.2);
        ConfigureResponsiveGrid(BottomGrid, width < 720, 1.25);

        var contentWidth = width < 850 ? width - 48 : width - 238;
        var cardWidth = width < 650 ? Math.Max(220, contentWidth) :
            width < 1050 ? Math.Max(220, (contentWidth - 16) / 2) :
            Math.Max(220, (contentWidth - 32) / 3);
        foreach (var child in TableCardsFlex.Children)
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

    private void ShowUserMenu(object sender, PointerEventArgs e) => UserMenu.IsVisible = true;
    private void HideUserMenu(object sender, PointerEventArgs e) => UserMenu.IsVisible = false;
    private void ToggleUserMenu(object sender, TappedEventArgs e) => UserMenu.IsVisible = !UserMenu.IsVisible;

    private void UpdateNavigationSelection(string selected)
    {
        UpdateButtons(Sidebar, selected);
        UpdateButtons(MobileNavigation, selected);
    }

    private static void UpdateButtons(Element root, string selected)
    {
        if (root is Button button && button.CommandParameter is string section)
        {
            var active = section.Equals(selected, StringComparison.OrdinalIgnoreCase);
            button.BackgroundColor = Color.FromArgb(active ? "#EAF3E6" : "#FFFFFF");
            button.TextColor = Color.FromArgb(active ? "#397D2A" : "#65716A");
            button.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            button.CornerRadius = 9;
        }
        if (root is IElementController controller)
            foreach (var child in controller.LogicalChildren)
                if (child is Element element) UpdateButtons(element, selected);
    }
}
