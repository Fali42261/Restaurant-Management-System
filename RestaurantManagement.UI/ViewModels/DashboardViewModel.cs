using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private string _feedbackMessage = "";
    private string _searchText = "";
    private string _selectedSection = "Dashboard";
    private CancellationTokenSource? _toastCts;
    private static readonly Dictionary<string,string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"]="Today's restaurant performance at a glance.",
        ["Orders"]="Review and manage online, dine-in and takeaway orders.",
        ["Tables"]="View table availability and seating status.",
        ["Menu"]="Manage dishes, categories, prices and availability.",
        ["Inventory"]="Track ingredients and stock levels.",
        ["Reservations"]="Manage bookings, guest details and reservation status.",
        ["Customers"]="View customer profiles and visit history.",
        ["Staff"]="Manage restaurant team members and roles.",
        ["Reports"]="Review daily sales and operational reports.",
        ["Settings"]="Configure restaurant and application preferences."
    };
    public DashboardViewModel()
    {
        Title="Dashboard";
        LogoutCommand=new Command(async()=>await Shell.Current.GoToAsync("//LoginPage"));
        NavigateCommand=new Command<string>(Navigate);
        TableActionCommand=new Command<string>(table=>ShowToast($"{table} selected. Table actions will be connected in the next UI step."));
        AddReservationCommand=new Command(()=>ShowToast("Reservation form will be added in the Reservations UI module."));
        SupportCommand=new Command(()=>ShowToast("Support center UI will be available in the Settings module."));
        NotificationCommand=new Command(()=>ShowToast("You are all caught up. No new notifications."));
    }
    public string FeedbackMessage { get=>_feedbackMessage; private set { if(SetProperty(ref _feedbackMessage,value)) OnPropertyChanged(nameof(HasFeedback)); } }
    public bool HasFeedback=>!string.IsNullOrWhiteSpace(FeedbackMessage);
    public string SearchText { get=>_searchText; set=>SetProperty(ref _searchText,value); }
    public string SelectedSection
    {
        get=>_selectedSection;
        private set { if(SetProperty(ref _selectedSection,value)) { OnPropertyChanged(nameof(SectionTitle)); OnPropertyChanged(nameof(SectionDescription)); OnPropertyChanged(nameof(IsDashboardSection)); OnPropertyChanged(nameof(IsOtherSection)); OnPropertyChanged(nameof(IsTablesSection)); } }
    }
    public string SectionTitle=>SelectedSection;
    public string SectionDescription=>Descriptions.TryGetValue(SelectedSection,out var value)?value:"Restaurant workspace";
    public bool IsDashboardSection=>SelectedSection.Equals("Dashboard",StringComparison.OrdinalIgnoreCase);
    public bool IsTablesSection => SelectedSection.Equals("Tables", StringComparison.OrdinalIgnoreCase);
    public bool IsOtherSection => !IsDashboardSection && !IsTablesSection;
    public ICommand LogoutCommand{get;}
    public ICommand NavigateCommand{get;}
    public ICommand TableActionCommand{get;}
    public ICommand AddReservationCommand{get;}
    public ICommand SupportCommand{get;}
    public ICommand NotificationCommand{get;}
    private void Navigate(string? section)
    {
        if(string.IsNullOrWhiteSpace(section)||!Descriptions.ContainsKey(section)) return;
        SelectedSection=section;
        ShowToast($"{section} section selected.");
    }
    private async void ShowToast(string message)
    {
        _toastCts?.Cancel();
        _toastCts?.Dispose();
        _toastCts=new CancellationTokenSource();
        StatusToast(message,_toastCts.Token);
    }
    private async void StatusToast(string message,CancellationToken token)
    {
        FeedbackMessage=message;
        try { await Task.Delay(2800,token); if(!token.IsCancellationRequested) FeedbackMessage=""; }
        catch(OperationCanceledException) { }
    }
}