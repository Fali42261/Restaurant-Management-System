using RestaurantManagement.UI.Models;

namespace RestaurantManagement.UI.Services.Interfaces;

public interface IAuthService
{
    Task<User?> SignInAsync(string identifier, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
