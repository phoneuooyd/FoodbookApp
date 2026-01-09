using Supabase.Gotrue;

namespace FoodbookApp.Services.Auth;

public sealed class SupabaseAuthService : ISupabaseAuthService
{
    private readonly Supabase.Client _client;
    private readonly IAuthTokenStore _tokenStore;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SupabaseAuthService(Supabase.Client client, IAuthTokenStore tokenStore)
    {
        _client = client;
        _tokenStore = tokenStore;
    }

    public Session? CurrentSession => _client.Auth.CurrentSession;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] Initializing Supabase client...");
            await _client.InitializeAsync();
            _initialized = true;
            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] Supabase client initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Initialization failed: {ex.Message}");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<Session> SignUpAsync(string email, string password)
    {
        await EnsureInitializedAsync();
        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignUp start: {email}");

        var session = await _client.Auth.SignUp(email, password);

        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignUp complete; session: {session != null}");
        await PersistTokenAsync(session);

        return session;
    }

    public async Task<Session> SignInAsync(string email, string password)
    {
        await EnsureInitializedAsync();
        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignIn start: {email}");

        var session = await _client.Auth.SignIn(email, password);

        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignIn complete; token present: {!string.IsNullOrWhiteSpace(session?.AccessToken)}");
        await PersistTokenAsync(session);

        return session;
    }

    public async Task SignOutAsync()
    {
        await EnsureInitializedAsync();
        try
        {
            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] SignOut start");
            await _client.Auth.SignOut();
            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] SignOut complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignOut error: {ex.Message}");
        }
        finally
        {
            await _tokenStore.ClearAsync();
        }
    }

    private async Task PersistTokenAsync(Session? session)
    {
        var accessToken = session?.AccessToken;
        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Persisting token: {!string.IsNullOrWhiteSpace(accessToken)}");
        await _tokenStore.SetAccessTokenAsync(accessToken);
    }
}
