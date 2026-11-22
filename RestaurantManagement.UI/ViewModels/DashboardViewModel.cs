using System.Collections.ObjectModel;
using System.Windows.Input;
using RestaurantManagement.UI.Models;
using RestaurantManagement.UI.Services;

namespace RestaurantManagement.UI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private string _searchText = "";
    private string _menuSearchText = "";
    private string _selectedMenuCategory = "All categories";
    private string _menuLoadStatus = "Loading menu items...";
    private string _selectedSection = "Dashboard";
    private int _orderSequence = 2000;
    private string _reportPeriod = "Today";
    private string _selectedFloorFilter = "All Floors";
    private decimal _posDiscountPercent;
    private decimal _posTaxPercent = 5m;
    private decimal _posDiscountAmount;
    private decimal _posTaxAmount;
    private string _todayRevenueText = "Loading...";
    private string _activeOrdersText = "—";
    private string _todayOrdersDetailText = "Loading live data";
    private string _occupiedTablesText = "—";
    private string _availableTablesText = "—";
    private string _todayReservationsText = "—";
    private string _upcomingGuestsText = "Loading live data";
    private string _dashboardDataStatus = "Loading live restaurant data...";

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"] = "Business performance and restaurant overview.",
        ["Orders"] = "Create, view, edit and delete restaurant orders.",
        ["Settings"] = "Manage your restaurant application preferences.",
        ["Subscription"] = "View the free trial and compare monthly, quarterly and yearly plans.",
        ["Help Support"] = "Get help with using swapDigit and contact support.",
        ["Tables"] = "Live table availability, order and dining status.",
        ["POS Billing"] = "Point of sale billing workspace.",
        ["Self Service"] = "Self-service ordering workspace.",
        ["KOT"] = "Kitchen order ticket queue.",
        ["Reservations"] = "Manage bookings, guest details and reservation status.",
        ["Sales / Bills"] = "Sales transactions and customer bills.",
        ["Store / Godown"] = "Store, godown and stock management.",
        ["Reports"] = "Review daily sales and operational reports.",
        ["Floors and Tables"] = "Manage restaurant floors and table setup.",
        ["Menu Management"] = "Manage restaurant menu items and categories.",
        ["Customers"] = "Manage restaurant customer information.",
        ["Staff Management"] = "Manage restaurant staff and access.",
        ["Inventory"] = "Manage restaurant stock and inventory."
    };

    public DashboardViewModel()
    {
        Title = "Business Dashboard";
        LogoutCommand = new Command(async () => await Shell.Current.GoToAsync("//LoginPage"));
        NavigateCommand = new Command<string>(Navigate);
        TableActionCommand = new Command<string>(_ => { });
        AddReservationCommand = new Command(() => { });
        ConfirmReservationCommand = new Command<ReservationRecord>(r => { if (r != null) UpdateReservationStatus(r, "Confirmed"); });
        CancelReservationCommand = new Command<ReservationRecord>(r => { if (r != null) UpdateReservationStatus(r, "Canceled"); });
        ViewBillCommand = new Command<BillRecord>(b => { if (b != null) SelectedBill = b; });
        MarkBillPaidCommand = new Command<BillRecord>(b => { if (b != null) MarkBillPaid(b); });
        SupportCommand = new Command(() => Navigate("Help Support"));
        NotificationCommand = new Command(() => { });

        Floors = new ObservableCollection<FloorRecord> { new("Ground Floor") };
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
        VisibleTables = new ObservableCollection<TableRecord>(Tables);
        FloorFilterOptions = new ObservableCollection<string> { "All Floors" };
        foreach (var floor in Floors) FloorFilterOptions.Add(floor.Name);
        Floors.CollectionChanged += (_, _) => RefreshFloorFilterOptions();
        Tables.CollectionChanged += (_, _) => { RefreshVisibleTables(); NotifyTableCounts(); };

        Orders = new ObservableCollection<OrderRecord>
        {
            new("ORD-1001", "Paneer Butter Masala, Roti", "Table 01", 3, "Preparing", DateTime.Now.AddMinutes(-12)),
            new("ORD-1002", "Veg Biryani", "Table 05", 2, "Order Placed", DateTime.Now.AddMinutes(-5)),
            new("ORD-1003", "Masala Dosa, Coffee", "Takeaway", 2, "Completed", DateTime.Now.AddMinutes(-38))
        };
        Orders.CollectionChanged += (_, _) => { NotifyOrderCounts(); NotifyReportSummary(); };
        Reservations = new ObservableCollection<ReservationRecord>
        {
            new("RES-1001", "Aarav Sharma", "9876543210", DateTime.Today, new TimeSpan(19, 0, 0), "T3", 4, "Confirmed"),
            new("RES-1002", "Priya Verma", "9876501234", DateTime.Today, new TimeSpan(19, 30, 0), "T5", 2, "Pending"),
            new("RES-1003", "Kabir Singh", "9812345670", DateTime.Today.AddDays(1), new TimeSpan(20, 0, 0), "T8", 5, "Confirmed")
        };
        Reservations.CollectionChanged += (_, _) => { NotifyReservationCounts(); NotifyReportSummary(); };
        Bills = new ObservableCollection<BillRecord>
        {
            new("INV-2001", "Aarav Sharma", "Table 01", 1240m, "Paid", DateTime.Now.AddHours(-2)),
            new("INV-2002", "Walk-in Guest", "Table 05", 860m, "Unpaid", DateTime.Now.AddMinutes(-45)),
            new("INV-2003", "Priya Verma", "Takeaway", 540m, "Paid", DateTime.Now.AddDays(-1))
        };
        Bills.CollectionChanged += (_, _) => { NotifySalesSummary(); NotifyReportSummary(); };
        SelfServiceProducts = new ObservableCollection<MenuProduct>();
        MenuManagementProducts = new ObservableCollection<MenuProduct>();
        FilteredMenuProducts = new ObservableCollection<MenuProduct>();
        MenuCategories = new ObservableCollection<string> { "All categories" };
        SelfServiceCart = new ObservableCollection<CartLine>();
        PosCart = new ObservableCollection<CartLine>();
        PosCart.CollectionChanged += (_, _) => RefreshPosTotals();
        AddPosToCartCommand = new Command<string>(AddPosToCart);
        RemovePosCartLineCommand = new Command<CartLine>(line => { if (line != null) PosCart.Remove(line); });
        SelfServiceCart.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CartTotalText));
        KitchenTickets = new ObservableCollection<KotTicket>();
        AddToCartCommand = new Command<string>(AddToCart);
        RemoveCartLineCommand = new Command<CartLine>(line => { if (line != null) SelfServiceCart.Remove(line); });
        PlaceSelfServiceOrderCommand = new Command(PlaceSelfServiceOrder);
        AdvanceKotCommand = new Command<KotTicket>(async ticket => await AdvanceKotAsync(ticket));
        RefreshKitchenCommand = new Command(async () => await LoadKitchenTicketsAsync());
        NotifyOrderCounts();
        _ = LoadDashboardSummaryAsync();
        _ = LoadKitchenTicketsAsync();
    }

    public string TodayRevenueText { get => _todayRevenueText; private set => SetProperty(ref _todayRevenueText, value); }
    public string ActiveOrdersText { get => _activeOrdersText; private set => SetProperty(ref _activeOrdersText, value); }
    public string TodayOrdersDetailText { get => _todayOrdersDetailText; private set => SetProperty(ref _todayOrdersDetailText, value); }
    public string OccupiedTablesText { get => _occupiedTablesText; private set => SetProperty(ref _occupiedTablesText, value); }
    public string AvailableTablesText { get => _availableTablesText; private set => SetProperty(ref _availableTablesText, value); }
    public string TodayReservationsText { get => _todayReservationsText; private set => SetProperty(ref _todayReservationsText, value); }
    public string UpcomingGuestsText { get => _upcomingGuestsText; private set => SetProperty(ref _upcomingGuestsText, value); }
    public string DashboardDataStatus { get => _dashboardDataStatus; private set => SetProperty(ref _dashboardDataStatus, value); }
    public ObservableCollection<DashboardTopProduct> DashboardTopProducts { get; } = new();
    private double _revenueBar1Height = 2, _revenueBar2Height = 2, _revenueBar3Height = 2, _revenueBar4Height = 2, _revenueBar5Height = 2, _revenueBar6Height = 2, _revenueBar7Height = 2;
    private string _revenueDay1 = "—", _revenueDay2 = "—", _revenueDay3 = "—", _revenueDay4 = "—", _revenueDay5 = "—", _revenueDay6 = "—", _revenueDay7 = "—";
    private string _revenueAxisTop = "0", _revenueAxis75 = "0", _revenueAxis50 = "0", _revenueAxis25 = "0";
    public double RevenueBar1Height { get => _revenueBar1Height; private set => SetProperty(ref _revenueBar1Height, value); }
    public double RevenueBar2Height { get => _revenueBar2Height; private set => SetProperty(ref _revenueBar2Height, value); }
    public double RevenueBar3Height { get => _revenueBar3Height; private set => SetProperty(ref _revenueBar3Height, value); }
    public double RevenueBar4Height { get => _revenueBar4Height; private set => SetProperty(ref _revenueBar4Height, value); }
    public double RevenueBar5Height { get => _revenueBar5Height; private set => SetProperty(ref _revenueBar5Height, value); }
    public double RevenueBar6Height { get => _revenueBar6Height; private set => SetProperty(ref _revenueBar6Height, value); }
    public double RevenueBar7Height { get => _revenueBar7Height; private set => SetProperty(ref _revenueBar7Height, value); }
    public string RevenueDay1 { get => _revenueDay1; private set => SetProperty(ref _revenueDay1, value); }
    public string RevenueDay2 { get => _revenueDay2; private set => SetProperty(ref _revenueDay2, value); }
    public string RevenueDay3 { get => _revenueDay3; private set => SetProperty(ref _revenueDay3, value); }
    public string RevenueDay4 { get => _revenueDay4; private set => SetProperty(ref _revenueDay4, value); }
    public string RevenueDay5 { get => _revenueDay5; private set => SetProperty(ref _revenueDay5, value); }
    public string RevenueDay6 { get => _revenueDay6; private set => SetProperty(ref _revenueDay6, value); }
    public string RevenueDay7 { get => _revenueDay7; private set => SetProperty(ref _revenueDay7, value); }
    public string RevenueAxisTop { get => _revenueAxisTop; private set => SetProperty(ref _revenueAxisTop, value); }
    public string RevenueAxis75 { get => _revenueAxis75; private set => SetProperty(ref _revenueAxis75, value); }
    public string RevenueAxis50 { get => _revenueAxis50; private set => SetProperty(ref _revenueAxis50, value); }
    public string RevenueAxis25 { get => _revenueAxis25; private set => SetProperty(ref _revenueAxis25, value); }
    private string _kitchenDataStatus = "Loading kitchen tickets...";
    public string KitchenDataStatus { get => _kitchenDataStatus; private set => SetProperty(ref _kitchenDataStatus, value); }

    private async Task LoadDashboardSummaryAsync()
    {
        try
        {
            var api = new AuthService();
            var summary = await api.GetDashboardSummaryAsync();
            if (summary is null) throw new InvalidOperationException("The dashboard API returned no data.");
            TodayRevenueText = $"₹{summary.TodaySales:N0}";
            ActiveOrdersText = summary.ActiveOrders.ToString();
            TodayOrdersDetailText = $"{summary.TodayOrders} orders today";
            OccupiedTablesText = $"{summary.OccupiedTables}/{summary.TotalTables}";
            AvailableTablesText = $"{summary.AvailableTables} available";
            TodayReservationsText = summary.TodayReservations.ToString();
            UpcomingGuestsText = $"{summary.UpcomingGuests} upcoming guests";

            var today = DateTime.Today;
            var from = today.AddDays(-6);
            var sales = await api.GetDashboardSalesAsync(from, today);
            var daily = Enumerable.Range(0, 7).Select(i => from.AddDays(i))
                .Select(day => new { Day = day, Amount = sales.FirstOrDefault(x => x.SaleDate.Date == day.Date)?.GrossSales ?? 0m })
                .ToArray();
            var max = daily.Max(x => x.Amount);
            var scale = max <= 0 ? 1m : max;
            RevenueBar1Height = (double)Math.Max(2m, daily[0].Amount / scale * 145m);
            RevenueBar2Height = (double)Math.Max(2m, daily[1].Amount / scale * 145m);
            RevenueBar3Height = (double)Math.Max(2m, daily[2].Amount / scale * 145m);
            RevenueBar4Height = (double)Math.Max(2m, daily[3].Amount / scale * 145m);
            RevenueBar5Height = (double)Math.Max(2m, daily[4].Amount / scale * 145m);
            RevenueBar6Height = (double)Math.Max(2m, daily[5].Amount / scale * 145m);
            RevenueBar7Height = (double)Math.Max(2m, daily[6].Amount / scale * 145m);
            RevenueDay1 = daily[0].Day.ToString("ddd");
            RevenueDay2 = daily[1].Day.ToString("ddd");
            RevenueDay3 = daily[2].Day.ToString("ddd");
            RevenueDay4 = daily[3].Day.ToString("ddd");
            RevenueDay5 = daily[4].Day.ToString("ddd");
            RevenueDay6 = daily[5].Day.ToString("ddd");
            RevenueDay7 = daily[6].Day.ToString("ddd");
            RevenueAxisTop = max.ToString("0");
            RevenueAxis75 = (max * .75m).ToString("0");
            RevenueAxis50 = (max * .50m).ToString("0");
            RevenueAxis25 = (max * .25m).ToString("0");

            var topProducts = await api.GetDashboardTopProductsAsync(from, today);
            DashboardTopProducts.Clear();
            foreach (var product in topProducts)
                DashboardTopProducts.Add(product);

            DashboardDataStatus = "Live KPIs, 7-day sales chart and popular products connected.";
        }
        catch
        {
            DashboardDataStatus = "Some dashboard data could not be loaded. Check API/database connection.";
        }
    }

    private async Task LoadKitchenTicketsAsync()
    {
        try
        {
            KitchenDataStatus = "Loading live kitchen tickets...";
            var rows = await new AuthService().GetKitchenTicketsAsync();
            KitchenTickets.Clear();
            foreach (var row in rows)
                KitchenTickets.Add(new KotTicket(row.TicketId, row.OrderNumber,
                    string.IsNullOrWhiteSpace(row.Location) ? row.OrderType : row.Location,
                    row.Items, row.Status, row.CreatedAt));
            KitchenDataStatus = rows.Count == 0
                ? "No kitchen tickets found for this restaurant."
                : $"Live kitchen tickets connected • {rows.Count} loaded";
            NotifyKitchenCounts();
        }
        catch
        {
            KitchenTickets.Clear();
            KitchenDataStatus = "Kitchen API unavailable. Tickets were not loaded.";
            NotifyKitchenCounts();
        }
    }

    private async Task AdvanceKotAsync(KotTicket? ticket)
    {
        if (ticket is null || ticket.TicketId == Guid.Empty) return;
        var next = ticket.Status switch
        {
            "New" => "Preparing",
            "Preparing" => "Ready",
            "Ready" => "Served",
            _ => ticket.Status
        };
        if (next == ticket.Status) return;
        try
        {
            await new AuthService().UpdateKitchenTicketStatusAsync(ticket.TicketId, next);
            ticket.SetStatus(next);
            KitchenDataStatus = $"Ticket {ticket.TicketNumber} updated to {next}.";
            NotifyKitchenCounts();
        }
        catch
        {
            KitchenDataStatus = $"Could not update {ticket.TicketNumber}. Database status was not changed.";
        }
    }

    private void NotifyKitchenCounts()
    {
        OnPropertyChanged(nameof(NewKotCount));
        OnPropertyChanged(nameof(PreparingKotCount));
        OnPropertyChanged(nameof(ReadyKotCount));
    }

    public ObservableCollection<TableRecord> Tables { get; }
    public ObservableCollection<TableRecord> VisibleTables { get; }
    public ObservableCollection<FloorRecord> Floors { get; }
    public ObservableCollection<string> FloorFilterOptions { get; }

    public string SelectedFloorFilter
    {
        get => _selectedFloorFilter;
        set { if (SetProperty(ref _selectedFloorFilter, value)) RefreshVisibleTables(); }
    }

    public bool AddFloor(string name)
    {
        var normalized = name?.Trim() ?? "";
        if (normalized.Length == 0 || Floors.Any(f => f.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))) return false;
        Floors.Add(new FloorRecord(normalized));
        RefreshFloorFilterOptions();
        return true;
    }

    public bool UpdateFloor(FloorRecord floor, string name)
    {
        var normalized = name?.Trim() ?? "";
        if (normalized.Length == 0 || Floors.Any(f => !ReferenceEquals(f, floor) && f.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))) return false;
        var old = floor.Name;
        floor.Name = normalized;
        foreach (var table in Tables.Where(t => t.FloorName == old)) table.FloorName = normalized;
        RefreshFloorFilterOptions();
        RefreshVisibleTables();
        return true;
    }

    public bool DeleteFloor(FloorRecord floor)
    {
        if (Floors.Count <= 1 || Tables.Any(t => t.FloorName == floor.Name)) return false;
        Floors.Remove(floor);
        if (SelectedFloorFilter == floor.Name) SelectedFloorFilter = "All Floors";
        RefreshFloorFilterOptions();
        return true;
    }

    public bool AddTable(string name, int seats, string floor)
    {
        var normalized = name?.Trim() ?? "";
        if (normalized.Length == 0 || seats < 1 ||
            Tables.Any(t => t.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ||
            !Floors.Any(f => f.Name == floor)) return false;
        Tables.Add(new TableRecord(normalized, seats, "Available", 0, "₹ 0.00", "Ready for guests", floor));
        NotifyTableCounts();
        return true;
    }

    public bool UpdateTable(TableRecord table, string name, int seats, string floor)
    {
        var normalized = name?.Trim() ?? "";
        if (normalized.Length == 0 || seats < 1 ||
            Tables.Any(t => !ReferenceEquals(t, table) && t.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ||
            !Floors.Any(f => f.Name == floor)) return false;
        table.UpdateDetails(normalized, seats, floor);
        RefreshVisibleTables();
        return true;
    }

    public void DeleteTable(TableRecord table)
    {
        Tables.Remove(table);
        NotifyTableCounts();
    }

    private void RefreshFloorFilterOptions()
    {
        var selected = SelectedFloorFilter;
        FloorFilterOptions.Clear();
        FloorFilterOptions.Add("All Floors");
        foreach (var floor in Floors) FloorFilterOptions.Add(floor.Name);
        if (!FloorFilterOptions.Contains(selected)) _selectedFloorFilter = "All Floors";
        OnPropertyChanged(nameof(SelectedFloorFilter));
        RefreshVisibleTables();
    }

    private void RefreshVisibleTables()
    {
        if (VisibleTables == null) return;
        var selected = _selectedFloorFilter;
        var filtered = selected == "All Floors" ? Tables.ToList() : Tables.Where(t => t.FloorName == selected).ToList();
        VisibleTables.Clear();
        foreach (var table in filtered) VisibleTables.Add(table);
    }
    public ObservableCollection<OrderRecord> Orders { get; }

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
                OnPropertyChanged(nameof(IsSubscriptionSection));
                OnPropertyChanged(nameof(IsHelpSupportSection));
                OnPropertyChanged(nameof(IsSelfServiceSection));
                OnPropertyChanged(nameof(IsKotSection));
                OnPropertyChanged(nameof(IsReservationsSection));
                OnPropertyChanged(nameof(IsSalesBillsSection));
                OnPropertyChanged(nameof(IsReportsSection));
                OnPropertyChanged(nameof(IsPosBillingSection));
                OnPropertyChanged(nameof(IsMenuManagementSection));
            }
        }
    }

    public string SectionTitle => SelectedSection switch
    {
        "Dashboard" => "Business Dashboard",
        "Tables" => "Table Dashboard",
        "Self Service" => "Self Services",
        "KOT" => "KOT (Kitchen)",
        "Reservations" => "Reservation",
        "Sales / Bills" => "Sale/Billing",
        "Store / Godown" => "Store/Godown",
        "Floors and Tables" => "Floor & Tables",
        _ => SelectedSection
    };
    public string SectionDescription => Descriptions.TryGetValue(SelectedSection, out var value) ? value : "Restaurant workspace";
    public bool IsDashboardSection => SelectedSection.Equals("Dashboard", StringComparison.OrdinalIgnoreCase);
    public bool IsTablesSection => SelectedSection.Equals("Tables", StringComparison.OrdinalIgnoreCase) || SelectedSection.Equals("Floors and Tables", StringComparison.OrdinalIgnoreCase);
    public bool IsOrdersSection => SelectedSection.Equals("Orders", StringComparison.OrdinalIgnoreCase);
    public bool IsSettingsSection => SelectedSection.Equals("Settings", StringComparison.OrdinalIgnoreCase);
    public bool IsSubscriptionSection => SelectedSection.Equals("Subscription", StringComparison.OrdinalIgnoreCase);
    public bool IsHelpSupportSection => SelectedSection.Equals("Help Support", StringComparison.OrdinalIgnoreCase);
    public bool IsSelfServiceSection => SelectedSection.Equals("Self Service", StringComparison.OrdinalIgnoreCase);
    public bool IsKotSection => SelectedSection.Equals("KOT", StringComparison.OrdinalIgnoreCase);
    public bool IsReservationsSection => SelectedSection.Equals("Reservations", StringComparison.OrdinalIgnoreCase);
    public bool IsSalesBillsSection => SelectedSection.Equals("Sales / Bills", StringComparison.OrdinalIgnoreCase);
    public bool IsReportsSection => SelectedSection.Equals("Reports", StringComparison.OrdinalIgnoreCase);
    public bool IsPosBillingSection => SelectedSection.Equals("POS Billing", StringComparison.OrdinalIgnoreCase);
    public bool IsMenuManagementSection => SelectedSection.Equals("Menu Management", StringComparison.OrdinalIgnoreCase);
    public bool IsOtherSection => !IsDashboardSection && !IsTablesSection && !IsOrdersSection && !IsSettingsSection && !IsSubscriptionSection && !IsHelpSupportSection && !IsSelfServiceSection && !IsKotSection && !IsReservationsSection && !IsSalesBillsSection && !IsReportsSection && !IsPosBillingSection && !IsMenuManagementSection;

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
    public ICommand AddToCartCommand { get; }
    public ICommand AddPosToCartCommand { get; }
    public ICommand RemovePosCartLineCommand { get; }
    public ICommand RemoveCartLineCommand { get; }
    public ICommand PlaceSelfServiceOrderCommand { get; }
    public ICommand AdvanceKotCommand { get; }
    public ICommand RefreshKitchenCommand { get; }
    public ICommand ConfirmReservationCommand { get; }
    public ICommand CancelReservationCommand { get; }
    public ICommand ViewBillCommand { get; }
    public ICommand MarkBillPaidCommand { get; }
    public ObservableCollection<ReservationRecord> Reservations { get; }
    public ObservableCollection<BillRecord> Bills { get; }
    public ReservationRecord? SelectedReservation { get; private set; }
    public BillRecord? SelectedBill { get => _selectedBill; private set => SetProperty(ref _selectedBill, value); }
    private BillRecord? _selectedBill;
    public int TodayReservationCount => Reservations.Count(r => r.ReservationDate.Date == DateTime.Today && r.Status != "Canceled");
    public int PendingReservationCount => Reservations.Count(r => r.Status == "Pending");
    public int ConfirmedReservationCount => Reservations.Count(r => r.Status == "Confirmed");
    public decimal TotalSales => Bills.Where(b => b.Status == "Paid").Sum(b => b.Amount);
    public string TotalSalesText => $"₹{TotalSales:N0}";
    public string ReportPeriod { get => _reportPeriod; set { if (SetProperty(ref _reportPeriod, value)) NotifyReportSummary(); } }
    public IReadOnlyList<string> ReportPeriodOptions { get; } = new[] { "Today", "Last 7 Days", "Last 30 Days" };
    private DateTime ReportStart => ReportPeriod switch { "Last 7 Days" => DateTime.Today.AddDays(-6), "Last 30 Days" => DateTime.Today.AddDays(-29), _ => DateTime.Today };
    private bool InReportRange(DateTime value) => value >= ReportStart && value < DateTime.Today.AddDays(1);
    public decimal ReportRevenue => Bills.Where(b => b.Status == "Paid" && InReportRange(b.CreatedAt)).Sum(b => b.Amount);
    public string ReportRevenueText => $"₹{ReportRevenue:N0}";
    public decimal ReportOutstanding => Bills.Where(b => b.Status == "Unpaid" && InReportRange(b.CreatedAt)).Sum(b => b.Amount);
    public string ReportOutstandingText => $"₹{ReportOutstanding:N0}";
    public int ReportPaidBills => Bills.Count(b => b.Status == "Paid" && InReportRange(b.CreatedAt));
    public int ReportUnpaidBills => Bills.Count(b => b.Status == "Unpaid" && InReportRange(b.CreatedAt));
    public int ReportOrderCount => Orders.Count(o => InReportRange(o.CreatedAt));
    public int ReportCompletedOrders => Orders.Count(o => InReportRange(o.CreatedAt) && o.Status == "Completed");
    public int ReportOpenOrders => Orders.Count(o => InReportRange(o.CreatedAt) && o.Status != "Completed" && o.Status != "Cancelled");
    public int ReportReservations => Reservations.Count(r => InReportRange(r.ReservationDate) && r.Status != "Canceled");
    public int ReportDineInOrders => Orders.Count(o => InReportRange(o.CreatedAt) && o.TableName.StartsWith("Table", StringComparison.OrdinalIgnoreCase));
    public int ReportTakeawayOrders => Orders.Count(o => InReportRange(o.CreatedAt) && o.TableName.Contains("Takeaway", StringComparison.OrdinalIgnoreCase));
    public int ReportSelfServiceOrders => Orders.Count(o => InReportRange(o.CreatedAt) && o.TableName.Contains("Self Service", StringComparison.OrdinalIgnoreCase));
    public double ReportDineInProgress => ReportOrderCount == 0 ? 0 : (double)ReportDineInOrders / ReportOrderCount;
    public double ReportTakeawayProgress => ReportOrderCount == 0 ? 0 : (double)ReportTakeawayOrders / ReportOrderCount;
    public double ReportSelfServiceProgress => ReportOrderCount == 0 ? 0 : (double)ReportSelfServiceOrders / ReportOrderCount;
    public string ReportAverageBillText => ReportPaidBills == 0 ? "₹0" : $"₹{ReportRevenue / ReportPaidBills:N0}";
    public int PaidBillCount => Bills.Count(b => b.Status == "Paid");
    public int UnpaidBillCount => Bills.Count(b => b.Status == "Unpaid");
    public ObservableCollection<MenuProduct> SelfServiceProducts { get; }
    public ObservableCollection<MenuProduct> MenuManagementProducts { get; }
    public ObservableCollection<MenuProduct> FilteredMenuProducts { get; }
    public ObservableCollection<string> MenuCategories { get; }
    public string MenuLoadStatus { get => _menuLoadStatus; private set => SetProperty(ref _menuLoadStatus, value); }
    public string MenuSearchText
    {
        get => _menuSearchText;
        set { if (SetProperty(ref _menuSearchText, value ?? "")) RefreshFilteredMenuProducts(); }
    }
    public string SelectedMenuCategory
    {
        get => _selectedMenuCategory;
        set { if (SetProperty(ref _selectedMenuCategory, value ?? "All categories")) RefreshFilteredMenuProducts(); }
    }
    public int MenuTotalCount => MenuManagementProducts.Count;
    public bool MenuEmpty => MenuManagementProducts.Count == 0;
    public bool MenuFilterEmpty => MenuManagementProducts.Count > 0 && FilteredMenuProducts.Count == 0;
    public int MenuAvailableCount => MenuManagementProducts.Count(p => p.IsAvailable);
    public int MenuUnavailableCount => MenuManagementProducts.Count(p => !p.IsAvailable);
    public int MenuCategoryCount => MenuManagementProducts.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public void SetMenuProducts(IEnumerable<MenuProduct> products)
    {
        MenuManagementProducts.Clear();
        foreach (var product in products) MenuManagementProducts.Add(product);

        MenuCategories.Clear();
        MenuCategories.Add("All categories");
        foreach (var category in MenuManagementProducts.Select(p => p.Category)
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c))
            MenuCategories.Add(category);

        if (!MenuCategories.Contains(SelectedMenuCategory))
            SelectedMenuCategory = "All categories";

        OnPropertyChanged(nameof(MenuTotalCount));
        OnPropertyChanged(nameof(MenuEmpty));
        OnPropertyChanged(nameof(MenuAvailableCount));
        OnPropertyChanged(nameof(MenuUnavailableCount));
        OnPropertyChanged(nameof(MenuCategoryCount));
        RefreshFilteredMenuProducts();
    }

    public void SetMenuLoadStatus(string status) => MenuLoadStatus = status;

    private void RefreshFilteredMenuProducts()
    {
        if (FilteredMenuProducts == null || MenuManagementProducts == null) return;
        var search = MenuSearchText.Trim();
        var category = SelectedMenuCategory;
        var filtered = MenuManagementProducts.Where(p =>
            (category == "All categories" || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(search) ||
             p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             p.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             p.Description.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
        FilteredMenuProducts.Clear();
        foreach (var product in filtered) FilteredMenuProducts.Add(product);
        OnPropertyChanged(nameof(MenuFilterEmpty));
    }
    public ObservableCollection<CartLine> SelfServiceCart { get; }
    public ObservableCollection<CartLine> PosCart { get; }
    public decimal PosSubtotal => PosCart.Sum(x => x.LineTotal);
    public string PosSubtotalText => $"₹{PosSubtotal:N2}";
    public decimal PosDiscountPercent { get => _posDiscountPercent; set { if (SetProperty(ref _posDiscountPercent, Math.Clamp(value, 0m, 100m))) RefreshPosTotals(); } }
    public decimal PosTaxPercent { get => _posTaxPercent; set { if (SetProperty(ref _posTaxPercent, Math.Clamp(value, 0m, 100m))) RefreshPosTotals(); } }
    public string PosDiscountAmountText => $"− ₹{_posDiscountAmount:N2}";
    public string PosTaxAmountText => $"+ ₹{_posTaxAmount:N2}";
    public decimal PosGrandTotal => Math.Max(0m, PosSubtotal - _posDiscountAmount + _posTaxAmount);
    public string PosGrandTotalText => $"₹{PosGrandTotal:N2}";
    public string PosCartCountText => $"{PosCart.Sum(x => x.Quantity)} items";
    public ObservableCollection<KotTicket> KitchenTickets { get; }
    public string CartTotalText => $"Cart total: ₹{SelfServiceCart.Sum(x => x.LineTotal):N0}";
    public int NewKotCount => KitchenTickets.Count(x => x.Status == "New");
    public int PreparingKotCount => KitchenTickets.Count(x => x.Status == "Preparing");
    public int ReadyKotCount => KitchenTickets.Count(x => x.Status == "Ready");


    private void AddPosToCart(string? productName)
    {
        var product = SelfServiceProducts.FirstOrDefault(x => x.Name == productName);
        if (product == null) return;
        var line = PosCart.FirstOrDefault(x => x.ProductName == product.Name);
        if (line == null) PosCart.Add(new CartLine(product.Name, product.Price));
        else line.Quantity++;
        RefreshPosTotals();
    }

    public void RefreshPosTotals()
    {
        var subtotal = PosCart.Sum(x => x.LineTotal);
        _posDiscountAmount = Math.Round(subtotal * PosDiscountPercent / 100m, 2);
        _posTaxAmount = Math.Round((subtotal - _posDiscountAmount) * PosTaxPercent / 100m, 2);
        OnPropertyChanged(nameof(PosSubtotal)); OnPropertyChanged(nameof(PosSubtotalText));
        OnPropertyChanged(nameof(PosDiscountAmountText)); OnPropertyChanged(nameof(PosTaxAmountText));
        OnPropertyChanged(nameof(PosGrandTotal)); OnPropertyChanged(nameof(PosGrandTotalText));
        OnPropertyChanged(nameof(PosCartCountText));
    }

    public bool CompletePosSale(string customer, string table, decimal discountPercent, decimal taxPercent, string paymentMethod, decimal cashReceived, out string message)
    {
        message = "";
        if (PosCart.Count == 0) { message = "Add at least one item to the cart."; return false; }
        if (string.IsNullOrWhiteSpace(customer) || string.IsNullOrWhiteSpace(table)) { message = "Enter customer name and table / order type."; return false; }
        if (discountPercent < 0 || discountPercent > 100 || taxPercent < 0 || taxPercent > 100) { message = "Discount and tax must be between 0 and 100."; return false; }
        var subtotal = PosCart.Sum(x => x.LineTotal);
        var discount = Math.Round(subtotal * discountPercent / 100m, 2);
        var tax = Math.Round((subtotal - discount) * taxPercent / 100m, 2);
        var total = subtotal - discount + tax;
        if (total <= 0) { message = "The payable amount must be greater than zero."; return false; }
        if (string.Equals(paymentMethod, "Cash", StringComparison.OrdinalIgnoreCase) && cashReceived < total)
        { message = $"Cash received is less than the payable amount (₹{total:N2})."; return false; }

        var invoice = $"INV-{++_orderSequence}";
        var items = string.Join(", ", PosCart.Select(x => $"{x.ProductName} × {x.Quantity}"));
        var method = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod;
        var received = method == "Cash" ? cashReceived : total;
        var bill = new BillRecord(invoice, customer.Trim(), table.Trim(), total, "Paid", DateTime.Now, method, subtotal, discount, tax, received, Math.Max(0m, received - total), items);
        Bills.Insert(0, bill);
        Orders.Insert(0, new OrderRecord($"ORD-{_orderSequence}", items, table.Trim(), PosCart.Sum(x => x.Quantity), "Completed", DateTime.Now));
        PosCart.Clear();
        NotifySalesSummary();
        NotifyOrderCounts();
        RefreshPosTotals();
        message = $"Payment recorded successfully. Invoice {invoice}. Change: ₹{bill.Change:N2}.";
        return true;
    }

    private void AddToCart(string? productName)
    {
        var product = SelfServiceProducts.FirstOrDefault(x => x.Name == productName);
        if (product == null) return;
        var line = SelfServiceCart.FirstOrDefault(x => x.ProductName == product.Name);
        if (line == null) SelfServiceCart.Add(new CartLine(product.Name, product.Price));
        else line.Quantity++;
    }

    private void PlaceSelfServiceOrder()
    {
        if (SelfServiceCart.Count == 0) return;
        var orderId = $"ORD-{++_orderSequence}";
        var summary = string.Join(", ", SelfServiceCart.Select(x => $"{x.ProductName} × {x.Quantity}"));
        var quantity = SelfServiceCart.Sum(x => x.Quantity);
        Orders.Insert(0, new OrderRecord(orderId, summary, "Self Service", quantity, "Order Placed", DateTime.Now));
        SelfServiceCart.Clear();
        OnPropertyChanged(nameof(CartTotalText));
    }

    public void AddReservation(string guestName, string phone, DateTime date, TimeSpan time, string table, int guests)
    {
        if (string.IsNullOrWhiteSpace(guestName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(table) || guests < 1) return;
        var number = 1000 + Reservations.Count + 1;
        Reservations.Insert(0, new ReservationRecord($"RES-{number}", guestName.Trim(), phone.Trim(), date.Date, time, table, guests, "Pending"));
        NotifyReservationCounts();
    }

    public void UpdateReservationStatus(ReservationRecord reservation, string status)
    {
        reservation.SetStatus(status);
        NotifyReservationCounts();
    }

    public void AddBill(string customer, string table, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(customer) || string.IsNullOrWhiteSpace(table) || amount <= 0) return;
        var number = 2000 + Bills.Count + 1;
        Bills.Insert(0, new BillRecord($"INV-{number}", customer.Trim(), table.Trim(), amount, "Unpaid", DateTime.Now));
        NotifySalesSummary();
    }

    public void MarkBillPaid(BillRecord bill)
    {
        bill.SetStatus("Paid");
        NotifySalesSummary();
    }

    private void NotifyReservationCounts()
    {
        OnPropertyChanged(nameof(TodayReservationCount));
        OnPropertyChanged(nameof(PendingReservationCount));
        OnPropertyChanged(nameof(ConfirmedReservationCount));
        NotifyReportSummary();
    }

    private void NotifyReportSummary()
    {
        OnPropertyChanged(nameof(ReportRevenue)); OnPropertyChanged(nameof(ReportRevenueText));
        OnPropertyChanged(nameof(ReportOutstanding)); OnPropertyChanged(nameof(ReportOutstandingText));
        OnPropertyChanged(nameof(ReportPaidBills)); OnPropertyChanged(nameof(ReportUnpaidBills));
        OnPropertyChanged(nameof(ReportOrderCount)); OnPropertyChanged(nameof(ReportCompletedOrders));
        OnPropertyChanged(nameof(ReportOpenOrders)); OnPropertyChanged(nameof(ReportReservations));
        OnPropertyChanged(nameof(ReportDineInOrders)); OnPropertyChanged(nameof(ReportTakeawayOrders));
        OnPropertyChanged(nameof(ReportSelfServiceOrders)); OnPropertyChanged(nameof(ReportDineInProgress));
        OnPropertyChanged(nameof(ReportTakeawayProgress)); OnPropertyChanged(nameof(ReportSelfServiceProgress));
        OnPropertyChanged(nameof(ReportAverageBillText));
    }

    private void NotifySalesSummary()
    {
        OnPropertyChanged(nameof(TotalSales));
        OnPropertyChanged(nameof(TotalSalesText));
        OnPropertyChanged(nameof(PaidBillCount));
        OnPropertyChanged(nameof(UnpaidBillCount));
        NotifyReportSummary();
    }



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
    }

    public void AddOrder(string item, string table, int quantity)
    {
        var number = $"ORD-{1001 + Orders.Count + DateTime.Now.Second}";
        Orders.Insert(0, new OrderRecord(number, item, table, quantity, "Order Placed", DateTime.Now));
        NotifyOrderCounts();
    }

    public void UpdateOrder(OrderRecord order, string item, string table, int quantity)
    {
        order.Update(item, table, quantity);
    }

    public void DeleteOrder(OrderRecord order)
    {
        Orders.Remove(order);
        NotifyOrderCounts();
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
        NotifyReportSummary();
    }

    private void Navigate(string? section)
    {
        if (string.IsNullOrWhiteSpace(section) || !Descriptions.ContainsKey(section)) return;
        SelectedSection = section;
        if (section.Equals("KOT", StringComparison.OrdinalIgnoreCase))
            _ = LoadKitchenTicketsAsync();
        else if (section.Equals("Dashboard", StringComparison.OrdinalIgnoreCase))
            _ = LoadDashboardSummaryAsync();
    }


}

public sealed class TableRecord : BaseViewModel
{
    private string _status;
    private string _timerText = "";
    private string _detailText;
    private string _floorName;
    private string _name;
    private int _seats;
    private DateTime _startedAt;

    public TableRecord(string name, int seats, string status, int elapsedMinutes, string amount, string detail, string floorName = "Ground Floor")
    {
        _name = name;
        _seats = seats;
        _floorName = floorName;
        AmountText = amount;
        _status = status;
        _detailText = detail;
        _startedAt = DateTime.Now.AddMinutes(-elapsedMinutes);
        RefreshElapsed();
    }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public int Seats { get => _seats; private set => SetProperty(ref _seats, value); }
    public string FloorName { get => _floorName; set => SetProperty(ref _floorName, value); }
    public string AmountText { get; }
    public string SeatsText => $"{Seats} seats";
    public void UpdateDetails(string name, int seats, string floor) { Name = name; Seats = seats; FloorName = floor; OnPropertyChanged(nameof(SeatsText)); }
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

public sealed class FloorRecord : BaseViewModel
{
    private string _name;
    public FloorRecord(string name) => _name = name;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
}


public sealed class MenuProduct
{
    public MenuProduct(string name, string category, decimal price, string description, bool isAvailable = true)
    { Name = name; Category = category; Price = price; Description = description; IsAvailable = isAvailable; }
    public string Name { get; }
    public string Category { get; }
    public decimal Price { get; }
    public string PriceText => $"₹{Price:N2}";
    public string Description { get; }
    public bool IsAvailable { get; }
    public string AvailabilityText => IsAvailable ? "Available" : "Unavailable";
}

public sealed class CartLine : BaseViewModel
{
    private int _quantity = 1;
    public CartLine(string productName, decimal unitPrice) { ProductName = productName; UnitPrice = unitPrice; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get => _quantity; set { if (SetProperty(ref _quantity, Math.Max(1, value))) { OnPropertyChanged(nameof(LineTotal)); OnPropertyChanged(nameof(LineTotalText)); } } }
    public decimal LineTotal => UnitPrice * Quantity;
    public string LineTotalText => $"₹{LineTotal:N0}";
}

public sealed class KotTicket : BaseViewModel
{
    private string _status;
    public KotTicket(Guid ticketId, string ticketNumber, string orderType, string items, string status, DateTime createdAt)
    { TicketId = ticketId; TicketNumber = ticketNumber; OrderType = orderType; Items = items; _status = status; CreatedAt = createdAt; }
    public KotTicket(string ticketNumber, string orderType, string items, string status, DateTime createdAt)
        : this(Guid.Empty, ticketNumber, orderType, items, status, createdAt) { }
    public Guid TicketId { get; }
    public string TicketNumber { get; }
    public string OrderType { get; }
    public string Items { get; }
    public DateTime CreatedAt { get; }
    public string AgeText => string.Format("{0} min ago", Math.Max(0, (int)(DateTime.Now - CreatedAt).TotalMinutes));
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) { OnPropertyChanged(nameof(ActionText)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(CanAdvance)); } } }
    public string ActionText => Status switch { "New" => "Start Preparing", "Preparing" => "Mark Ready", "Ready" => "Mark Served", "Served" => "Served", "Cancelled" => "Cancelled", _ => "Completed" };
    public bool CanAdvance => Status is "New" or "Preparing" or "Ready";
    public string StatusColor => Status switch { "New" => "#B7791F", "Preparing" => "#2563A6", "Ready" => "#17804A", _ => "#64748B" };
    public void SetStatus(string status) => Status = status;
    public void Advance() { Status = Status switch { "New" => "Preparing", "Preparing" => "Ready", "Ready" => "Served", _ => "Served" }; }
}


public sealed class ReservationRecord : BaseViewModel
{
    private string _status;
    public ReservationRecord(string reservationId, string guestName, string phone, DateTime reservationDate, TimeSpan reservationTime, string table, int guests, string status)
    {
        ReservationId = reservationId; GuestName = guestName; Phone = phone; ReservationDate = reservationDate; ReservationTime = reservationTime; Table = table; Guests = guests; _status = status;
    }
    public string ReservationId { get; }
    public string GuestName { get; }
    public string Phone { get; }
    public DateTime ReservationDate { get; }
    public TimeSpan ReservationTime { get; }
    public string DateText => ReservationDate.ToString("dd MMM yyyy");
    public string TimeText => DateTime.Today.Add(ReservationTime).ToString("hh:mm tt");
    public string Table { get; }
    public int Guests { get; }
    public string GuestCountText => $"{Guests} guests";
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusColor)); } }
    public string StatusColor => Status switch { "Confirmed" => "#17804A", "Pending" => "#B7791F", _ => "#B94B45" };
    public void SetStatus(string status) => Status = status;
}

public sealed class BillRecord : BaseViewModel
{
    private string _status;
    public BillRecord(string invoiceNumber, string customer, string table, decimal amount, string status, DateTime createdAt,
        string paymentMethod = "Unspecified", decimal subtotal = 0m, decimal discount = 0m, decimal tax = 0m,
        decimal cashReceived = 0m, decimal change = 0m, string items = "")
    {
        InvoiceNumber = invoiceNumber; Customer = customer; Table = table; Amount = amount; _status = status; CreatedAt = createdAt;
        PaymentMethod = paymentMethod; Subtotal = subtotal == 0m ? amount : subtotal; Discount = discount; Tax = tax;
        CashReceived = cashReceived; Change = change; Items = items;
    }
    public string InvoiceNumber { get; }
    public string Customer { get; }
    public string Table { get; }
    public decimal Amount { get; }
    public string AmountText => $"₹{Amount:N2}";
    public DateTime CreatedAt { get; }
    public string CreatedText => CreatedAt.ToString("dd MMM, hh:mm tt");
    public string PaymentMethod { get; }
    public decimal Subtotal { get; }
    public decimal Discount { get; }
    public decimal Tax { get; }
    public decimal CashReceived { get; }
    public decimal Change { get; }
    public string Items { get; }
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) { OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(CanMarkPaid)); } } }
    public bool CanMarkPaid => Status.Equals("Unpaid", StringComparison.OrdinalIgnoreCase);
    public string StatusColor => Status switch { "Paid" => "#17804A", "Cancelled" => "#B94B45", _ => "#B7791F" };
    public void SetStatus(string status) => Status = status;
}
