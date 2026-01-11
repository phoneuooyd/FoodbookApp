using Microsoft.Maui.Storage;

namespace FoodbookApp.Services.Auth;

public sealed class SecureStorageAuthTokenStore : IAuthTokenStore
{
    private const string ActiveAccountKey = "auth.active_account_id";

    private static string AccessTokenKey(Guid accountId) => $"sb:{accountId}:access_token";
    private static string RefreshTokenKey(Guid accountId) => $"sb:{accountId}:refresh_token";
    private static string ExpiresAtKey(Guid accountId) => $"sb:{accountId}:expires_at";

    public async Task<Guid?> GetActiveAccountIdAsync()
    {
        try
        {
            var raw = await SecureStorage.GetAsync(ActiveAccountKey);
            return Guid.TryParse(raw, out var id) ? id : (Guid?)null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetActiveAccountIdAsync(Guid? accountId)
    {
        try
        {
            if (accountId == null || accountId == Guid.Empty)
            {
                SecureStorage.Remove(ActiveAccountKey);
                return;
            }

            await SecureStorage.SetAsync(ActiveAccountKey, accountId.Value.ToString());
        }
        catch
        {
        }
    }

    public Task<string?> GetAccessTokenAsync(Guid accountId) => GetAsync(AccessTokenKey(accountId));

    public Task<string?> GetRefreshTokenAsync(Guid accountId) => GetAsync(RefreshTokenKey(accountId));

    public async Task<DateTimeOffset?> GetExpiresAtAsync(Guid accountId)
    {
        var raw = await GetAsync(ExpiresAtKey(accountId));
        return DateTimeOffset.TryParse(raw, out var dto) ? dto : null;
    }

    public async Task SetTokensAsync(Guid accountId, string? accessToken, string? refreshToken, DateTimeOffset? expiresAt)
    {
        try
        {
            if (accountId == Guid.Empty)
                return;

            if (string.IsNullOrWhiteSpace(accessToken))
                SecureStorage.Remove(AccessTokenKey(accountId));
            else
                await SecureStorage.SetAsync(AccessTokenKey(accountId), accessToken);

            if (string.IsNullOrWhiteSpace(refreshToken))
                SecureStorage.Remove(RefreshTokenKey(accountId));
            else
                await SecureStorage.SetAsync(RefreshTokenKey(accountId), refreshToken);

            if (expiresAt == null)
                SecureStorage.Remove(ExpiresAtKey(accountId));
            else
                await SecureStorage.SetAsync(ExpiresAtKey(accountId), expiresAt.Value.ToString("O"));
        }
        catch
        {
        }
    }

    public Task ClearTokensAsync(Guid accountId)
    {
        try
        {
            if (accountId != Guid.Empty)
            {
                SecureStorage.Remove(AccessTokenKey(accountId));
                SecureStorage.Remove(RefreshTokenKey(accountId));
                SecureStorage.Remove(ExpiresAtKey(accountId));
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        try
        {
            SecureStorage.Remove(ActiveAccountKey);
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private static async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch
        {
            return null;
        }
    }
}
