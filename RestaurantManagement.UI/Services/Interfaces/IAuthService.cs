using RestaurantManagement.UI.Models;

namespace RestaurantManagement.UI.Services.Interfaces;

public interface IAuthService
{
    Task RegisterOwnerAsync(OwnerRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<User> SignInAsync(string identifier, string password, CancellationToken cancellationToken = default);
    Task<List<RestaurantInfo>> GetRestaurantsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RestaurantInfo> CreateRestaurantAsync(CreateRestaurantInput input, CancellationToken cancellationToken = default);
    Task SelectRestaurantAsync(RestaurantInfo restaurant, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
