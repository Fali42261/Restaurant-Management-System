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
            {
                UpdateNavigationSelection(vm.SelectedSection);
                FindByName<Border>("UserMenu").IsVisible = false;
            }
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
        FindByName<Grid>("Sidebar").IsVisible = !compact;
        FindByName<ScrollView>("MobileNavigation").IsVisible = compact;
        FindByName<Grid>("RootLayout").ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(190);
        FindByName<Label>("WelcomeLabel").IsVisible = width >= 850;
        FindByName<Border>("SearchPanel").IsVisible = width >= 1000;
        FindByName<Border>("UserMenu").IsVisible = false;

        FindByName<Grid>("StatsGrid").ColumnDefinitions.Clear();
        var statsColumns = width < 650 ? 2 : 4;
        for (var i = 0; i < statsColumns; i++)
            FindByName<Grid>("StatsGrid").ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < FindByName<Grid>("StatsGrid").Children.Count; i++)
        {
            Grid.SetColumn((BindableObject)FindByName<Grid>("StatsGrid").Children[i], i % statsColumns);
            Grid.SetRow((BindableObject)FindByName<Grid>("StatsGrid").Children[i], i / statsColumns);
        }

        ConfigureResponsiveGrid(FindByName<Grid>("AnalyticsGrid"), width < 720, 1.2);
        ConfigureResponsiveGrid(FindByName<Grid>("BottomGrid"), width < 720, 1.25);

        var contentWidth = width < 850 ? width - 48 : width - 238;
        var cardWidth = width < 650 ? Math.Max(220, contentWidth) :
            width < 1050 ? Math.Max(220, (contentWidth - 16) / 2) :
            Math.Max(220, (contentWidth - 32) / 3);
        foreach (var child in FindByName<FlexLayout>("TableCardsFlex").Children)
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

    private void ShowUserMenu(object sender, PointerEventArgs e) => FindByName<Border>("UserMenu").IsVisible = true;
    private void HideUserMenu(object sender, PointerEventArgs e) => FindByName<Border>("UserMenu").IsVisible = false;
    private void ToggleUserMenu(object sender, TappedEventArgs e) => FindByName<Border>("UserMenu").IsVisible = !FindByName<Border>("UserMenu").IsVisible;

    private void UpdateNavigationSelection(string selected)
    {
        UpdateButtons(FindByName<Grid>("Sidebar"), selected);
        UpdateButtons(FindByName<ScrollView>("MobileNavigation"), selected);
    }

    private static void UpdateButtons(Element root, string selected)
    {
        if (root is Button button && button.CommandParameter is string section)
        {
            var active = section.Equals(selected, StringComparison.OrdinalIgnoreCase);
            button.BackgroundColor = Color.FromArgb(active ? "#FFFFFF" : "#0D6971");
            button.TextColor = Color.FromArgb(active ? "#075B63" : "#E8F2F2");
            button.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            button.CornerRadius = 9;
        }
        if (root is IElementController controller)
            foreach (var child in controller.LogicalChildren)
                if (child is Element element) UpdateButtons(element, selected);
    }
}
