using Supabase.Gotrue;

namespace FoodbookApp.Services.Auth;

public sealed class SupabaseAuthService : ISupabaseAuthService
{
    private readonly Supabase.Client _client;
    private readonly IAuthTokenStore _tokenStore;

    public SupabaseAuthService(Supabase.Client client, IAuthTokenStore tokenStore)
    {
        _client = client;
        _tokenStore = tokenStore;
    }

    public Session? CurrentSession => _client.Auth.CurrentSession;

    public async Task<Session> SignUpAsync(string email, string password)
    {
        var session = await _client.Auth.SignUp(email, password);
        await PersistTokenAsync(session);
        return session;
    }

    public async Task<Session> SignInAsync(string email, string password)
    {
        var session = await _client.Auth.SignIn(email, password);
        await PersistTokenAsync(session);
        return session;
    }

    public async Task SignOutAsync()
    {
        try
        {
            await _client.Auth.SignOut();
        }
        finally
        {
            await _tokenStore.ClearAsync();
        }
    }

    private async Task PersistTokenAsync(Session session)
    {
        // Supabase access token is what you send as Authorization Bearer
        var accessToken = session?.AccessToken;
        await _tokenStore.SetAccessTokenAsync(accessToken);
    }
}
