using Microsoft.Maui.Storage;

namespace RestaurantManagement.UI.Services;

/// <summary>
/// Device-local trial gate for the current UI preview.
/// Production enforcement must also be validated by the authenticated server.
/// </summary>
public static class TrialAccessService
{
    private const string TrialStartKey = "swapdigit_trial_started_utc";
    private const string SubscriptionActiveKey = "swapdigit_subscription_active";

    public static void StartTrialIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(Preferences.Default.Get(TrialStartKey, string.Empty)))
            Preferences.Default.Set(TrialStartKey, DateTimeOffset.UtcNow.ToString("O"));
    }

    public static bool HasAccess
    {
        get
        {
            if (Preferences.Default.Get(SubscriptionActiveKey, false))
                return true;

            var stored = Preferences.Default.Get(TrialStartKey, string.Empty);
            if (!DateTimeOffset.TryParse(stored, out var started))
                return true;

            return DateTimeOffset.UtcNow < started.AddDays(3);
        }
    }

    // Call only after a verified successful payment from the backend.
    public static void MarkSubscriptionActiveAfterVerifiedPayment()
        => Preferences.Default.Set(SubscriptionActiveKey, true);
}
