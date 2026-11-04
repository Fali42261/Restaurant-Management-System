using Microsoft.AspNetCore.Identity;
using RestaurantManagement.API.Configuration;
using RestaurantManagement.API.Models;
using RestaurantManagement.API.Repositories;
using RestaurantManagement.API.Services;

namespace RestaurantManagement.API.Tests;

public sealed class AuthServiceTests
{
    private static (AuthService Service, FakeAuthRepository Repository) CreateService()
    {
        var settings = new AppSettings
        {
            Jwt = new JwtSettings
            {
                Issuer = "RestaurantManagement.Tests",
                Audience = "RestaurantManagement.Tests.Clients",
                SigningKey = "test-signing-key-with-at-least-32-characters",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 30
            },
            Auth = new AuthSettings { PasswordMinimumLength = 10 }
        };

        var repository = new FakeAuthRepository();
        var hasher = new PasswordHasher<AuthUser>();
        var tokens = new TokenService(settings);
        return (new AuthService(repository, hasher, tokens, settings), repository);
    }

    private static RegisterRequest ValidRegistration(
        string email = "staff@example.com",
        string mobile = "9876543210") =>
        new("Test Staff", email, "+91", mobile, "SecurePassword123!");

    [Fact]
    public async Task Register_NormalizesIdentityAndAssignsStaffRole()
    {
        var (service, repository) = CreateService();

        var result = await service.Register(ValidRegistration(), CancellationToken.None);

        Assert.Equal("staff@example.com", result.User.Email);
        Assert.Equal("+91", result.User.CountryCode);
        Assert.Equal("+919876543210", result.User.MobileNumber);
        Assert.Equal(new[] { "Staff" }, result.User.Roles);
        Assert.NotEqual("SecurePassword123!", repository.Users.Single().PasswordHash);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task Register_RejectsPasswordBelowConfiguredMinimum()
    {
        var (service, repository) = CreateService();
        var request = new RegisterRequest("Test Staff", "staff@example.com", null, null, "short");

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.Register(request, CancellationToken.None));

        Assert.Contains("at least 10 characters", error.Message);
        Assert.Empty(repository.Users);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        var (service, _) = CreateService();
        await service.Register(ValidRegistration(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<DuplicateAccountException>(
            () => service.Register(ValidRegistration("STAFF@example.com", "9123456780"), CancellationToken.None));

        Assert.Equal("AUTH_EMAIL_ALREADY_EXISTS", error.ErrorCode);
    }

    [Fact]
    public async Task Register_RejectsDuplicateMobile()
    {
        var (service, _) = CreateService();
        await service.Register(ValidRegistration(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<DuplicateAccountException>(
            () => service.Register(ValidRegistration("another@example.com", "9876543210"), CancellationToken.None));

        Assert.Equal("AUTH_MOBILE_ALREADY_EXISTS", error.ErrorCode);
    }

    [Fact]
    public async Task Login_AcceptsEmailAndInternationalMobile()
    {
        var (service, _) = CreateService();
        await service.Register(ValidRegistration(), CancellationToken.None);

        var byEmail = await service.Login(new LoginRequest("STAFF@example.com", "SecurePassword123!"), CancellationToken.None);
        var byMobile = await service.Login(new LoginRequest("+919876543210", "SecurePassword123!"), CancellationToken.None);

        Assert.Equal(1, byEmail.User.UserId);
        Assert.Equal(1, byMobile.User.UserId);
        Assert.Equal("Staff", Assert.Single(byMobile.User.Roles));
    }

    [Fact]
    public async Task Login_RejectsIncorrectPassword()
    {
        var (service, _) = CreateService();
        await service.Register(ValidRegistration(), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.Login(new LoginRequest("staff@example.com", "WrongPassword123!"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_RejectsInactiveAccount()
    {
        var (service, repository) = CreateService();
        await service.Register(ValidRegistration(), CancellationToken.None);
        repository.Users.Single().IsActive = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.Login(new LoginRequest("staff@example.com", "SecurePassword123!"), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_RotatesRefreshTokenAndRejectsReuse()
    {
        var (service, _) = CreateService();
        var registered = await service.Register(ValidRegistration(), CancellationToken.None);

        var renewed = await service.Renew(registered.RefreshToken, CancellationToken.None);

        Assert.NotEqual(registered.RefreshToken, renewed.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.Renew(registered.RefreshToken, CancellationToken.None));

        var renewedAgain = await service.Renew(renewed.RefreshToken, CancellationToken.None);
        Assert.Equal(registered.User.UserId, renewedAgain.User.UserId);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var (service, _) = CreateService();
        var registered = await service.Register(ValidRegistration(), CancellationToken.None);

        await service.Logout(registered.RefreshToken, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.Renew(registered.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Me_ReturnsCurrentUserAndRoles()
    {
        var (service, _) = CreateService();
        var registered = await service.Register(ValidRegistration(), CancellationToken.None);

        var user = await service.Me(registered.User.UserId, CancellationToken.None);

        Assert.Equal(registered.User.UserId, user.UserId);
        Assert.Equal("staff@example.com", user.Email);
        Assert.Equal(new[] { "Staff" }, user.Roles);
    }
}

internal sealed class FakeAuthRepository : IAuthRepository
{
    private readonly Dictionary<long, AuthUser> _users = [];
    private readonly Dictionary<string, long> _refreshTokens = new(StringComparer.Ordinal);
    private long _nextId;

    public IReadOnlyCollection<AuthUser> Users => _users.Values;

    public Task<AuthUser?> FindByEmailOrMobileAsync(string identifier, CancellationToken ct)
    {
        var user = _users.Values.FirstOrDefault(x =>
            string.Equals(x.Email, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.MobileNumber, identifier, StringComparison.Ordinal));
        return Task.FromResult(user);
    }

    public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var user = _users.Values.FirstOrDefault(x =>
            string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<AuthUser?> FindByIdAsync(long id, CancellationToken ct)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user is { IsActive: true } ? user : null);
    }

    public Task<long> CreateUserAsync(AuthUser user, string refreshHash, DateTime expiry, CancellationToken ct)
    {
        var id = ++_nextId;
        user.UserId = id;
        _users[id] = user;
        _refreshTokens[refreshHash] = id;
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<string>> GetRolesAsync(long id, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(_users.ContainsKey(id) ? ["Staff"] : []);

    public Task<bool> StoreRefreshTokenAsync(long id, string hash, DateTime expiry, CancellationToken ct)
    {
        if (!_users.TryGetValue(id, out var user) || !user.IsActive)
            return Task.FromResult(false);

        _refreshTokens[hash] = id;
        return Task.FromResult(true);
    }

    public Task<long?> RotateRefreshTokenAsync(string oldHash, string newHash, DateTime expiry, CancellationToken ct)
    {
        if (!_refreshTokens.Remove(oldHash, out var id))
            return Task.FromResult<long?>(null);

        _refreshTokens[newHash] = id;
        return Task.FromResult<long?>(id);
    }

    public Task RevokeRefreshTokenAsync(string hash, CancellationToken ct)
    {
        _refreshTokens.Remove(hash);
        return Task.CompletedTask;
    }

    public Task UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken ct)
    {
        if (_users.TryGetValue(userId, out var user))
            user.PasswordHash = passwordHash;

        return Task.CompletedTask;
    }
}
