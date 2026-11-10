using System.Collections.ObjectModel;
using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private string _feedbackMessage = "";
    private string _searchText = "";
    private string _selectedSection = "Dashboard";
    private CancellationTokenSource? _toastCts;

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"] = "Business performance and restaurant overview.",
        ["Orders"] = "Create, view, edit and delete restaurant orders.",
        ["Settings"] = "Manage your restaurant application preferences.",
        ["Help Support"] = "Get help with using swapDigit and contact support.",
        ["Tables"] = "Live table availability, order and dining status.",
        ["POS Billing"] = "Point of sale billing workspace.",
        ["Self Service"] = "Self-service ordering workspace.",
        ["KOT"] = "Kitchen order ticket queue.",
        ["Reservations"] = "Manage bookings, guest details and reservation status.",
        ["Sales / Bills"] = "Sales transactions and customer bills.",
        ["Store / Godown"] = "Store, godown and stock management.",
        ["Reports"] = "Review daily sales and operational reports.",
        ["Floors & Tables"] = "Manage restaurant floors and table setup."
    };

    public DashboardViewModel()
    {
        Title = "Business Dashboard";
        LogoutCommand = new Command(async () => await Shell.Current.GoToAsync("//LoginPage"));
        NavigateCommand = new Command<string>(Navigate);
        TableActionCommand = new Command<string>(table => ShowToast($"{table} selected."));
        AddReservationCommand = new Command(() => ShowToast("Reservation form will be available in Reservations."));
        SupportCommand = new Command(() => Navigate("Help Support"));
        NotificationCommand = new Command(() => ShowToast("You are all caught up. No new notifications."));

        Tables = new ObservableCollection<TableRecord>
        {
            new("T1", 4, "Dining", 18, "₹ 0.00", "1 KOT • 1 item"),
            new("T2", 4, "Available", 0, "₹ 0.00", "Ready for guests"),
            new("T3", 4, "Reserved", 0, "₹ 0.00", "Booking at 7:30 PM"),
            new("T4", 4, "Available", 0, "₹ 0.00", "Ready for guests"),
            new("T5", 4, "Order Placed", 5, "₹ 640.00", "2 items • Waiting for kitchen"),
            new("T6", 4, "Preparing", 12, "₹ 920.00", "3 items • Kitchen working"),
            new("T7", 4, "Available", 0, "₹ 0.00", "Ready for guests"),
            new("T8", 4, "Served", 28, "₹ 1,250.00", "Food served • Dining"),
            new("T9", 4, "Bill Pending", 3, "₹ 780.00", "Request received"),
            new("T10", 4, "Available", 0, "₹ 0.00", "Ready for guests"),
            new("T11", 4, "Ready", 9, "₹ 560.00", "Order ready to serve"),
            new("T12", 4, "Available", 0, "₹ 0.00", "Ready for guests")
        };

        Orders = new ObservableCollection<OrderRecord>
        {
            new("ORD-1001", "Paneer Butter Masala, Roti", "Table 01", 3, "Preparing", DateTime.Now.AddMinutes(-12)),
            new("ORD-1002", "Veg Biryani", "Table 05", 2, "Order Placed", DateTime.Now.AddMinutes(-5)),
            new("ORD-1003", "Masala Dosa, Coffee", "Takeaway", 2, "Completed", DateTime.Now.AddMinutes(-38))
        };
        Orders.CollectionChanged += (_, _) => NotifyOrderCounts();
        NotifyOrderCounts();
    }

    public ObservableCollection<TableRecord> Tables { get; }
    public ObservableCollection<OrderRecord> Orders { get; }

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        private set { if (SetProperty(ref _feedbackMessage, value)) OnPropertyChanged(nameof(HasFeedback)); }
    }

    public bool HasFeedback => !string.IsNullOrWhiteSpace(FeedbackMessage);
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }

    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(SectionTitle));
                OnPropertyChanged(nameof(SectionDescription));
                OnPropertyChanged(nameof(IsDashboardSection));
                OnPropertyChanged(nameof(IsOtherSection));
                OnPropertyChanged(nameof(IsTablesSection));
                OnPropertyChanged(nameof(IsOrdersSection));
                OnPropertyChanged(nameof(IsSettingsSection));
                OnPropertyChanged(nameof(IsHelpSupportSection));
            }
        }
    }

    public string SectionTitle => SelectedSection;
    public string SectionDescription => Descriptions.TryGetValue(SelectedSection, out var value) ? value : "Restaurant workspace";
    public bool IsDashboardSection => SelectedSection.Equals("Dashboard", StringComparison.OrdinalIgnoreCase);
    public bool IsTablesSection => SelectedSection.Equals("Tables", StringComparison.OrdinalIgnoreCase) || SelectedSection.Equals("Floors & Tables", StringComparison.OrdinalIgnoreCase);
    public bool IsOrdersSection => SelectedSection.Equals("Orders", StringComparison.OrdinalIgnoreCase);
    public bool IsSettingsSection => SelectedSection.Equals("Settings", StringComparison.OrdinalIgnoreCase);
    public bool IsHelpSupportSection => SelectedSection.Equals("Help Support", StringComparison.OrdinalIgnoreCase);
    public bool IsOtherSection => !IsDashboardSection && !IsTablesSection && !IsOrdersSection && !IsSettingsSection && !IsHelpSupportSection;

    public int AvailableTableCount => Tables.Count(t => t.Status == "Available");
    public int ReservedTableCount => Tables.Count(t => t.Status == "Reserved");
    public int ActiveTableCount => Tables.Count(t => t.Status is "Order Placed" or "Preparing" or "Ready" or "Served" or "Dining" or "Bill Pending");
    public int ReadyTableCount => Tables.Count(t => t.Status == "Ready");
    public int TotalOrderCount => Orders.Count;
    public int ActiveOrderCount => Orders.Count(o => o.Status != "Completed" && o.Status != "Cancelled");
    public int CompletedOrderCount => Orders.Count(o => o.Status == "Completed");

    public ICommand LogoutCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand TableActionCommand { get; }
    public ICommand AddReservationCommand { get; }
    public ICommand SupportCommand { get; }
    public ICommand NotificationCommand { get; }

    public void RefreshTableTimers()
    {
        foreach (var table in Tables) table.RefreshElapsed();
    }

    public void AdvanceTable(TableRecord table)
    {
        switch (table.Status)
        {
            case "Available":
            case "Reserved":
                table.SetStatus("Order Placed");
                break;
            case "Order Placed":
                table.SetStatus("Preparing");
                break;
            case "Preparing":
                table.SetStatus("Ready");
                break;
            case "Ready":
                table.SetStatus("Served");
                break;
            case "Served":
                table.SetStatus("Dining");
                break;
            case "Dining":
                table.SetStatus("Bill Pending");
                break;
            case "Bill Pending":
                table.SetStatus("Available");
                break;
            default:
                table.SetStatus("Available");
                break;
        }
        NotifyTableCounts();
        ShowToast($"{table.Name}: {table.Status}");
    }

    public void AddOrder(string item, string table, int quantity)
    {
        var number = $"ORD-{1001 + Orders.Count + DateTime.Now.Second}";
        Orders.Insert(0, new OrderRecord(number, item, table, quantity, "Order Placed", DateTime.Now));
        NotifyOrderCounts();
        ShowToast("Order added successfully.");
    }

    public void UpdateOrder(OrderRecord order, string item, string table, int quantity)
    {
        order.Update(item, table, quantity);
        ShowToast($"{order.OrderNumber} updated.");
    }

    public void DeleteOrder(OrderRecord order)
    {
        Orders.Remove(order);
        NotifyOrderCounts();
        ShowToast($"{order.OrderNumber} deleted.");
    }

    private void NotifyTableCounts()
    {
        OnPropertyChanged(nameof(AvailableTableCount));
        OnPropertyChanged(nameof(ReservedTableCount));
        OnPropertyChanged(nameof(ActiveTableCount));
        OnPropertyChanged(nameof(ReadyTableCount));
    }

    private void NotifyOrderCounts()
    {
        OnPropertyChanged(nameof(TotalOrderCount));
        OnPropertyChanged(nameof(ActiveOrderCount));
        OnPropertyChanged(nameof(CompletedOrderCount));
    }

    private void Navigate(string? section)
    {
        if (string.IsNullOrWhiteSpace(section) || !Descriptions.ContainsKey(section)) return;
        SelectedSection = section;
        ShowToast($"{section} selected.");
    }

    private void ShowToast(string message)
    {
        _toastCts?.Cancel();
        _toastCts?.Dispose();
        _toastCts = new CancellationTokenSource();
        StatusToast(message, _toastCts.Token);
    }

    private async void StatusToast(string message, CancellationToken token)
    {
        FeedbackMessage = message;
        try { await Task.Delay(2800, token); if (!token.IsCancellationRequested) FeedbackMessage = ""; }
        catch (OperationCanceledException) { }
    }
}

public sealed class TableRecord : BaseViewModel
{
    private string _status;
    private string _timerText = "";
    private string _detailText;
    private DateTime _startedAt;

    public TableRecord(string name, int seats, string status, int elapsedMinutes, string amount, string detail)
    {
        Name = name;
        Seats = seats;
        AmountText = amount;
        _status = status;
        _detailText = detail;
        _startedAt = DateTime.Now.AddMinutes(-elapsedMinutes);
        RefreshElapsed();
    }

    public string Name { get; }
    public int Seats { get; }
    public string AmountText { get; }
    public string SeatsText => $"{Seats} seats";
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) { OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(CardColor)); OnPropertyChanged(nameof(BorderBrush)); OnPropertyChanged(nameof(ActionText)); } } }
    public string TimerText { get => _timerText; private set => SetProperty(ref _timerText, value); }
    public string DetailText { get => _detailText; private set => SetProperty(ref _detailText, value); }
    public string ActionText => Status switch { "Available" => "Start Order", "Reserved" => "Seat Guest", "Order Placed" => "Send to KOT", "Preparing" => "Mark Ready", "Ready" => "Serve", "Served" => "Start Dining", "Dining" => "Request Bill", "Bill Pending" => "Checkout", _ => "Next" };
    public string StatusColor => Status switch { "Available" => "#368C55", "Reserved" => "#B17B16", "Order Placed" => "#23658A", "Preparing" => "#8A5BB5", "Ready" => "#B45B21", "Served" or "Dining" => "#075B63", "Bill Pending" => "#B94B45", _ => "#536159" };
    public Color CardColor => Color.FromArgb(Status is "Served" or "Dining" ? "#DDF1F7" : Status == "Available" ? "#F4F7F7" : "#E8F4FA");
    public Brush BorderBrush => new SolidColorBrush(Color.FromArgb(Status == "Available" ? "#CFDADD" : Status == "Reserved" ? "#E5C86A" : "#A8D3E6"));

    public void SetStatus(string status)
    {
        Status = status;
        _startedAt = DateTime.Now;
        DetailText = status switch
        {
            "Order Placed" => "Order received • Waiting for kitchen",
            "Preparing" => "KOT sent • Kitchen preparing",
            "Ready" => "Order ready to serve",
            "Served" => "Food served to guest",
            "Dining" => "Guest is dining",
            "Bill Pending" => "Payment requested",
            "Available" => "Ready for next guest",
            _ => "Table reserved"
        };
        RefreshElapsed();
    }

    public void RefreshElapsed()
    {
        var elapsed = DateTime.Now - _startedAt;
        TimerText = Status == "Available" ? "No active order" : $"{Math.Max(0, (int)elapsed.TotalMinutes)} min • {Status}";
    }
}

public sealed class OrderRecord : BaseViewModel
{
    private string _itemSummary;
    private string _tableName;
    private int _quantity;

    public OrderRecord(string orderNumber, string itemSummary, string tableName, int quantity, string status, DateTime createdAt)
    {
        OrderNumber = orderNumber;
        _itemSummary = itemSummary;
        _tableName = tableName;
        _quantity = quantity;
        Status = status;
        CreatedAt = createdAt;
    }

    public string OrderNumber { get; }
    public string ItemSummary { get => _itemSummary; private set => SetProperty(ref _itemSummary, value); }
    public string TableName { get => _tableName; private set => SetProperty(ref _tableName, value); }
    public int Quantity { get => _quantity; private set => SetProperty(ref _quantity, value); }
    public string QuantityText => $"Qty: {Quantity}";
    public string Status { get; }
    public DateTime CreatedAt { get; }
    public string CreatedText => CreatedAt.ToString("dd MMM, hh:mm tt");

    public void Update(string item, string table, int quantity)
    {
        ItemSummary = item;
        TableName = table;
        Quantity = quantity;
        OnPropertyChanged(nameof(QuantityText));
    }
}
