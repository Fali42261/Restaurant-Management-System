using RestaurantManagement.UI.ViewModels;

namespace RestaurantManagement.UI.Views;

public partial class DashboardPage : ContentPage
{
    private double _lastWidth = -1;

    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = new DashboardViewModel();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0 || Math.Abs(width - _lastWidth) < 1)
            return;

        _lastWidth = width;
        var isCompact = width < 850;

        Sidebar.IsVisible = !isCompact;
        MobileNavigation.IsVisible = isCompact;
        RootLayout.ColumnDefinitions[0].Width = isCompact ? new GridLength(0) : new GridLength(190);
        SearchPanel.IsVisible = width >= 650;

        if (width < 650)
        {
            StatsGrid.ColumnDefinitions.Clear();
            StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            StatsGrid.Children[0].SetValue(Grid.ColumnProperty, 0);
            StatsGrid.Children[1].SetValue(Grid.ColumnProperty, 1);
            StatsGrid.Children[2].SetValue(Grid.ColumnProperty, 0);
            StatsGrid.Children[2].SetValue(Grid.RowProperty, 1);
            StatsGrid.Children[3].SetValue(Grid.ColumnProperty, 1);
            StatsGrid.Children[3].SetValue(Grid.RowProperty, 1);
        }
        else
        {
            StatsGrid.ColumnDefinitions.Clear();
            for (var i = 0; i < 4; i++)
                StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < 4; i++)
            {
                StatsGrid.Children[i].SetValue(Grid.ColumnProperty, i);
                StatsGrid.Children[i].SetValue(Grid.RowProperty, 0);
            }
        }

        var stackSections = width < 720;
        AnalyticsGrid.ColumnDefinitions.Clear();
        BottomGrid.ColumnDefinitions.Clear();
        if (stackSections)
        {
            AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalyticsGrid.Children[0].SetValue(Grid.ColumnProperty, 0);
            AnalyticsGrid.Children[1].SetValue(Grid.ColumnProperty, 0);
            AnalyticsGrid.Children[1].SetValue(Grid.RowProperty, 1);
            AnalyticsGrid.RowDefinitions.Clear();
            AnalyticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AnalyticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            BottomGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            BottomGrid.Children[0].SetValue(Grid.ColumnProperty, 0);
            BottomGrid.Children[1].SetValue(Grid.ColumnProperty, 0);
            BottomGrid.Children[1].SetValue(Grid.RowProperty, 1);
            BottomGrid.RowDefinitions.Clear();
            BottomGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            BottomGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        else
        {
            AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)));
            AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalyticsGrid.RowDefinitions.Clear();
            AnalyticsGrid.Children[0].SetValue(Grid.ColumnProperty, 0);
            AnalyticsGrid.Children[1].SetValue(Grid.ColumnProperty, 1);
            AnalyticsGrid.Children[1].SetValue(Grid.RowProperty, 0);

            BottomGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.25, GridUnitType.Star)));
            BottomGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            BottomGrid.RowDefinitions.Clear();
            BottomGrid.Children[0].SetValue(Grid.ColumnProperty, 0);
            BottomGrid.Children[1].SetValue(Grid.ColumnProperty, 1);
            BottomGrid.Children[1].SetValue(Grid.RowProperty, 0);
        }
    }
}