using RestaurantManagement.UI.Models;

namespace RestaurantManagement.UI.Services.Interfaces;

public interface IAuthService
{
    Task<string> RegisterOwnerAsync(OwnerRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<string> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<User> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<List<RestaurantInfo>> GetRestaurantsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RestaurantInfo> CreateRestaurantAsync(CreateRestaurantInput input, CancellationToken cancellationToken = default);
    Task SelectRestaurantAsync(RestaurantInfo restaurant, CancellationToken cancellationToken = default);
    Task<DashboardSummary?> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
