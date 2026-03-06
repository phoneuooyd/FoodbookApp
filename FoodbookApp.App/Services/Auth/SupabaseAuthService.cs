using Supabase.Gotrue;
using Foodbook.Data;
using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services.Auth;

public sealed class SupabaseAuthService : ISupabaseAuthService
{
    private readonly global::Supabase.Client _client;
    private readonly IAuthTokenStore _tokenStore;
    private readonly AppDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SupabaseAuthService(
        global::Supabase.Client client, 
        IAuthTokenStore tokenStore, 
        AppDbContext db,
        IServiceProvider serviceProvider)
    {
        _client = client;
        _tokenStore = tokenStore;
        _db = db;
        _serviceProvider = serviceProvider;
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
        await PersistSessionAsync(session, email);

        return session;
    }

    public async Task<Session> SignInAsync(string email, string password)
    {
        await EnsureInitializedAsync();
        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignIn start: {email}");

        var session = await _client.Auth.SignIn(email, password);

        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SignIn complete; token present: {!string.IsNullOrWhiteSpace(session?.AccessToken)}");
        await PersistSessionAsync(session, email);

        // Run deduplication in background after successful login - fire and forget
        // This is purely an optimization, login should not wait for it
        _ = Task.Run(async () => await RunDeduplicationSafeAsync());

        return session;
    }

    /// <summary>
    /// Runs deduplication service in background after login.
    /// This method NEVER throws - all errors are logged and swallowed.
    /// </summary>
    private async Task RunDeduplicationSafeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] Starting background deduplication...");
            
            IDeduplicationService? deduplicationService = null;
            try
            {
                deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Failed to get DeduplicationService: {ex.Message}");
                return;
            }

            if (deduplicationService == null)
            {
                System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] DeduplicationService not available (this is OK for new installations)");
                return;
            }

            // Fetch cloud data - handle empty cloud (new user) gracefully
            try
            {
                System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] Fetching cloud data for deduplication...");
                await deduplicationService.FetchCloudDataAsync();
                System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] Cloud data fetched successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Cloud data fetch failed (non-fatal): {ex.Message}");
                // Continue - empty cache is valid for new users
            }
            
            // Deduplicate sync queue
            try
            {
                var removed = await deduplicationService.DeduplicateSyncQueueAsync();
                System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Deduplication complete: {removed} duplicates removed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Deduplication failed (non-fatal): {ex.Message}");
                // This is fine - sync will proceed without deduplication
            }
        }
        catch (Exception ex)
        {
            // Catch-all for any unexpected errors
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] RunDeduplicationSafeAsync unexpected error: {ex.Message}");
        }
    }

    public async Task<Session?> SetSessionAsync(string accessToken, string refreshToken)
    {
        await EnsureInitializedAsync();
        System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] SetSessionAsync start - restoring session from tokens");

        try
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] SetSessionAsync: tokens are empty - cannot restore");
                return null;
            }

            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] SetSessionAsync: calling _client.Auth.SetSession...");

            // Use Supabase client to set session from tokens
            var session = await _client.Auth.SetSession(accessToken, refreshToken);

            if (session != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SetSessionAsync SUCCESS: user email={session.User?.Email ?? "unknown"}, has access token: {!string.IsNullOrWhiteSpace(session.AccessToken)}, has refresh token: {!string.IsNullOrWhiteSpace(session.RefreshToken)}");
                
                // Verify the session is actually valid by checking CurrentSession
                var currentSession = _client.Auth.CurrentSession;
                System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SetSessionAsync: CurrentSession after restore: {(currentSession != null ? "SET" : "NULL")}");

                // Update stored tokens with potentially refreshed ones
                var activeId = await _tokenStore.GetActiveAccountIdAsync();
                if (activeId.HasValue && !string.IsNullOrWhiteSpace(session.AccessToken))
                {
                    await _tokenStore.SetTokensAsync(activeId.Value, session.AccessToken, session.RefreshToken, null);
                    System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SetSessionAsync: Updated stored tokens for account {activeId.Value}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SetSessionAsync: Could not update tokens - activeId: {activeId}, hasAccessToken: {!string.IsNullOrWhiteSpace(session.AccessToken)}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] SetSessionAsync: SetSession returned null session - token restore failed");
            }

            return session;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SetSessionAsync FAILED: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] SetSessionAsync error details: {ex}");
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        await EnsureInitializedAsync();

        var active = await _tokenStore.GetActiveAccountIdAsync();

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
            if (active.HasValue)
                await _tokenStore.ClearTokensAsync(active.Value);

            await _tokenStore.SetActiveAccountIdAsync(null);

            // Clear deduplication cache on logout
            try
            {
                var deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
                deduplicationService?.ClearCache();
            }
            catch { }
        }
    }

    private async Task PersistSessionAsync(Session? session, string? email)
    {
        if (session == null) return;

        var sbUserId = session.User?.Id ?? _client.Auth.CurrentUser?.Id;
        if (string.IsNullOrWhiteSpace(sbUserId))
        {
            System.Diagnostics.Debug.WriteLine("[SupabaseAuthService] PersistSessionAsync: No Supabase user ID available");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] PersistSessionAsync: Processing user {sbUserId}");

        var account = await _db.AuthAccounts.FirstOrDefaultAsync(a => a.SupabaseUserId == sbUserId);
        if (account == null)
        {
            account = new Foodbook.Models.AuthAccount
            {
                Id = Guid.NewGuid(),
                SupabaseUserId = sbUserId,
                Email = email,
                CreatedUtc = DateTime.UtcNow,
                LastSignInUtc = DateTime.UtcNow,
                IsAutoLoginEnabled = true // ? Enable auto-login by default for new accounts
            };
            _db.AuthAccounts.Add(account);
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Created new account with auto-login enabled: {account.Id}");
        }
        else
        {
            account.Email ??= email;
            account.LastSignInUtc = DateTime.UtcNow;
            account.IsAutoLoginEnabled = true; // ? Re-enable auto-login on every successful login
            System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Updated existing account {account.Id} - auto-login enabled");
        }

        await _db.SaveChangesAsync();

        // ExpiresAt shape differs between Gotrue SDK versions; keep it optional.
        DateTimeOffset? expiresAt = null;

        await _tokenStore.SetTokensAsync(account.Id, session.AccessToken, session.RefreshToken, expiresAt);
        await _tokenStore.SetActiveAccountIdAsync(account.Id);

        System.Diagnostics.Debug.WriteLine($"[SupabaseAuthService] Session persisted for account {account.Id} with IsAutoLoginEnabled={account.IsAutoLoginEnabled}");

        // NOTE: user_preferences should NOT be fetched/created during login.
        // It is handled after the user enables sync and chooses priority in ProfilePage.
    }
}
