using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private string _feedbackMessage = "";
    private string _searchText = "";
    private string _selectedPeriod = "Monthly";
    private string _selectedSection = "Dashboard";
    private static readonly Dictionary<string,string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"]="Restaurant performance at a glance.",
        ["Orders"]="Review and manage online, dine-in and takeaway orders.",
        ["Tables"]="View table availability and seating status.",
        ["Menu"]="Manage dishes, categories, prices and availability.",
        ["Inventory"]="Track ingredients and stock levels.",
        ["Reservations"]="Manage bookings, guest details and reservation status.",
        ["Customers"]="View customer profiles and visit history.",
        ["Staff"]="Manage restaurant team members and roles.",
        ["Reports"]="Review sales, revenue and operational reports.",
        ["Settings"]="Configure restaurant and application preferences."
    };
    public DashboardViewModel()
    {
        Title="Dashboard";
        LogoutCommand=new Command(async()=>await Shell.Current.GoToAsync("//LoginPage"));
        NavigateCommand=new Command<string>(Navigate);
        ExportCommand=new Command(()=>ShowFeedback("Sample dashboard report is ready to export in the UI preview."));
        InsightsCommand=new Command(()=>ShowFeedback("Insights preview: revenue is trending up 3.5% this period."));
        AddReservationCommand=new Command(()=>ShowFeedback("Reservation form will be added in the Reservations UI module."));
        SupportCommand=new Command(()=>ShowFeedback("Support center UI will be available in the Settings module."));
        NotificationCommand=new Command(()=>ShowFeedback("You are all caught up. No new notifications."));
        PeriodCommand=new Command(()=>SelectedPeriod=SelectedPeriod=="Monthly"?"Weekly":SelectedPeriod=="Weekly"?"Daily":"Monthly");
        ChartPeriodCommand=new Command<string>(period=>ShowFeedback($"Revenue chart period changed to {period}."));
    }
    public string FeedbackMessage { get=>_feedbackMessage; private set { if(SetProperty(ref _feedbackMessage,value)) OnPropertyChanged(nameof(HasFeedback)); } }
    public bool HasFeedback=>!string.IsNullOrWhiteSpace(FeedbackMessage);
    public string SearchText { get=>_searchText; set=>SetProperty(ref _searchText,value); }
    public string SelectedPeriod { get=>_selectedPeriod; set=>SetProperty(ref _selectedPeriod,value); }
    public string SelectedSection
    {
        get=>_selectedSection;
        private set { if(SetProperty(ref _selectedSection,value)) { OnPropertyChanged(nameof(SectionTitle)); OnPropertyChanged(nameof(SectionDescription)); OnPropertyChanged(nameof(IsDashboardSection)); OnPropertyChanged(nameof(IsOtherSection)); } }
    }
    public string SectionTitle=>SelectedSection;
    public string SectionDescription=>Descriptions.TryGetValue(SelectedSection,out var value)?value:"Restaurant workspace";
    public bool IsDashboardSection=>SelectedSection.Equals("Dashboard",StringComparison.OrdinalIgnoreCase);
    public bool IsOtherSection=>!IsDashboardSection;
    public ICommand LogoutCommand{get;}
    public ICommand NavigateCommand{get;}
    public ICommand ExportCommand{get;}
    public ICommand InsightsCommand{get;}
    public ICommand AddReservationCommand{get;}
    public ICommand SupportCommand{get;}
    public ICommand NotificationCommand{get;}
    public ICommand PeriodCommand{get;}
    public ICommand ChartPeriodCommand{get;}
    private void Navigate(string? section) { if(string.IsNullOrWhiteSpace(section)||!Descriptions.ContainsKey(section)) return; SelectedSection=section; FeedbackMessage=""; }
    private void ShowFeedback(string message)=>FeedbackMessage=message;
}