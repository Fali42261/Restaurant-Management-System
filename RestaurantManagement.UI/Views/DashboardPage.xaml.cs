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
            Grid.SetColumn((BindableObject)StatsGrid.Children[0], 0);
            Grid.SetColumn((BindableObject)StatsGrid.Children[1], 1);
            Grid.SetColumn((BindableObject)StatsGrid.Children[2], 0);
            Grid.SetRow((BindableObject)StatsGrid.Children[2], 1);
            Grid.SetColumn((BindableObject)StatsGrid.Children[3], 1);
            Grid.SetRow((BindableObject)StatsGrid.Children[3], 1);
        }
        else
        {
            StatsGrid.ColumnDefinitions.Clear();
            for (var i = 0; i < 4; i++)
                StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < 4; i++)
            {
                Grid.SetColumn((BindableObject)StatsGrid.Children[i], i);
                Grid.SetRow((BindableObject)StatsGrid.Children[i], 0);
            }
        }

        var stackSections = width < 720;
        AnalyticsGrid.ColumnDefinitions.Clear();
        BottomGrid.ColumnDefinitions.Clear();
        if (stackSections)
        {
            AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn((BindableObject)AnalyticsGrid.Children[0], 0);
            Grid.SetColumn((BindableObject)AnalyticsGrid.Children[1], 0);
            Grid.SetRow((BindableObject)AnalyticsGrid.Children[1], 1);
            AnalyticsGrid.RowDefinitions.Clear();
            AnalyticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AnalyticsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            BottomGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn((BindableObject)BottomGrid.Children[0], 0);
            Grid.SetColumn((BindableObject)BottomGrid.Children[1], 0);
            Grid.SetRow((BindableObject)BottomGrid.Children[1], 1);
            BottomGrid.RowDefinitions.Clear();
            BottomGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            BottomGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        else
        {
            AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)));
            AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AnalyticsGrid.RowDefinitions.Clear();
            Grid.SetColumn((BindableObject)AnalyticsGrid.Children[0], 0);
            Grid.SetColumn((BindableObject)AnalyticsGrid.Children[1], 1);
            Grid.SetRow((BindableObject)AnalyticsGrid.Children[1], 0);

            BottomGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.25, GridUnitType.Star)));
            BottomGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            BottomGrid.RowDefinitions.Clear();
            Grid.SetColumn((BindableObject)BottomGrid.Children[0], 0);
            Grid.SetColumn((BindableObject)BottomGrid.Children[1], 1);
            Grid.SetRow((BindableObject)BottomGrid.Children[1], 0);
        }
    }
}