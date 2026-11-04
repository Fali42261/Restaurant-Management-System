using RestaurantManagement.API.Models;
namespace RestaurantManagement.API.Repositories;
public interface IAuthRepository{
 Task<AuthUser?> FindByEmailOrMobileAsync(string identifier,CancellationToken ct);
 Task<AuthUser?> FindByEmailAsync(string email,CancellationToken ct);
 Task<AuthUser?> FindByIdAsync(long id,CancellationToken ct);
 Task<long> CreateUserAsync(AuthUser user,string refreshHash,DateTime refreshExpiry,CancellationToken ct);
 Task<IReadOnlyList<string>> GetRolesAsync(long id,CancellationToken ct);
 Task<bool> StoreRefreshTokenAsync(long id,string hash,DateTime expiry,CancellationToken ct);
 Task<long?> RotateRefreshTokenAsync(string oldHash,string newHash,DateTime expiry,CancellationToken ct);
 Task RevokeRefreshTokenAsync(string hash,CancellationToken ct);
 Task UpdatePasswordHashAsync(long userId,string passwordHash,CancellationToken ct);
}