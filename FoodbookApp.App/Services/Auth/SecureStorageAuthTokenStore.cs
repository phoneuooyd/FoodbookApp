using Microsoft.Maui.Storage;

namespace FoodbookApp.Services.Auth;

public sealed class SecureStorageAuthTokenStore : IAuthTokenStore
{
    private const string AccessTokenKey = "auth.access_token";

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(AccessTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAccessTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                SecureStorage.Remove(AccessTokenKey);
                return;
            }

            await SecureStorage.SetAsync(AccessTokenKey, token);
        }
        catch
        {
            // ignore (device may not support secure storage in some scenarios)
        }
    }

    public Task ClearAsync()
    {
        try
        {
            SecureStorage.Remove(AccessTokenKey);
        }
        catch
        {
            // ignore
        }

        return Task.CompletedTask;
    }
}
