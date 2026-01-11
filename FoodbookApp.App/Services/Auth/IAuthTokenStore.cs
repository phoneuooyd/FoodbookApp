namespace FoodbookApp.Services.Auth;

public interface IAuthTokenStore
{
    Task<Guid?> GetActiveAccountIdAsync();
    Task SetActiveAccountIdAsync(Guid? accountId);

    Task<string?> GetAccessTokenAsync(Guid accountId);
    Task<string?> GetRefreshTokenAsync(Guid accountId);
    Task<DateTimeOffset?> GetExpiresAtAsync(Guid accountId);

    Task SetTokensAsync(Guid accountId, string? accessToken, string? refreshToken, DateTimeOffset? expiresAt);

    Task ClearTokensAsync(Guid accountId);
    Task ClearAllAsync();
}
