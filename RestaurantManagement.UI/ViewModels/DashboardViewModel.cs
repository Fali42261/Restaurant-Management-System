using System.Windows.Input;

namespace RestaurantManagement.UI.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private string _feedbackMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedPeriod = "Monthly";
    public DashboardViewModel()
    {
        Title = "Dashboard";
        LogoutCommand = new Command(async () =>
            await Shell.Current.GoToAsync("//LoginPage"));
        NavigateCommand = new Command<string>(Navigate);
        ExportCommand = new Command(() => ShowFeedback("Sample dashboard report is ready to export in the UI preview."));
        InsightsCommand = new Command(() => ShowFeedback("Insights preview: revenue is trending up 3.5% this period."));
        AddReservationCommand = new Command(() => ShowFeedback("Reservation form will be added in the Reservations UI module."));
        SupportCommand = new Command(() => ShowFeedback("Support center UI will be available in the Settings module."));
        NotificationCommand = new Command(() => ShowFeedback("You are all caught up. No new notifications."));
        PeriodCommand = new Command(() =>
            SelectedPeriod = SelectedPeriod == "Monthly" ? "Weekly" :
                             SelectedPeriod == "Weekly" ? "Daily" : "Monthly");
        ChartPeriodCommand = new Command<string>(period =>
            ShowFeedback($"Revenue chart period changed to {period}."));
    }

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        private set
        {
            if (SetProperty(ref _feedbackMessage, value))
                OnPropertyChanged(nameof(HasFeedback));
        }
    }

    public bool HasFeedback => !string.IsNullOrWhiteSpace(FeedbackMessage);

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set => SetProperty(ref _selectedPeriod, value);
    }

    public ICommand LogoutCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand InsightsCommand { get; }
    public ICommand AddReservationCommand { get; }
    public ICommand SupportCommand { get; }
    public ICommand NotificationCommand { get; }
    public ICommand PeriodCommand { get; }
    public ICommand ChartPeriodCommand { get; }

    private void Navigate(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;

        if (section.Equals("Dashboard", StringComparison.OrdinalIgnoreCase))
            ShowFeedback("Dashboard overview is selected.");
        else
            ShowFeedback($"{section} screen is part of the upcoming UI development phase.");
    }

    private void ShowFeedback(string message) => FeedbackMessage = message;
}