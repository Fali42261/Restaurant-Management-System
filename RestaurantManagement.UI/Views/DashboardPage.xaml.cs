using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class DashboardPage : ContentPage
{
    private double _lastWidth = -1;
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
        Loaded += (_, _) => UpdateNavigationSelection(vm.SelectedSection);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _lastWidth) < 1) return;
        _lastWidth = width;
        var compact = width < 850;
        Sidebar.IsVisible = !compact;
        MobileNavigation.IsVisible = compact;
        RootLayout.ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(190);
        WelcomeLabel.IsVisible = width >= 850;\n        SearchPanel.IsVisible = width >= 1000;\n        ProfilePanel.IsVisible = true;\n        LogoutButton.IsVisible = true;
        StatsGrid.ColumnDefinitions.Clear();
        var count = width < 650 ? 2 : 4;
        for (var i = 0; i < count; i++) StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < StatsGrid.Children.Count; i++)
        {
            Grid.SetColumn((BindableObject)StatsGrid.Children[i], i % count);
            Grid.SetRow((BindableObject)StatsGrid.Children[i], i / count);
        }
        ConfigureResponsiveGrid(AnalyticsGrid, width < 720, 1.2);
        ConfigureResponsiveGrid(BottomGrid, width < 720, 1.25);
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