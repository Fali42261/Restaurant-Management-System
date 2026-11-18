using RestaurantManagement.UI.Models;

namespace RestaurantManagement.UI.Services.Interfaces;

public interface IAuthService
{
    Task RegisterOwnerAsync(OwnerRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<User> SignInAsync(string identifier, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
