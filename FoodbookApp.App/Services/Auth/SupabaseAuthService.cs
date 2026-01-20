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
            return;

        var account = await _db.AuthAccounts.FirstOrDefaultAsync(a => a.SupabaseUserId == sbUserId);
        if (account == null)
        {
            account = new AuthAccount
            {
                Id = Guid.NewGuid(),
                SupabaseUserId = sbUserId,
                Email = email,
                CreatedUtc = DateTime.UtcNow,
                LastSignInUtc = DateTime.UtcNow
            };
            _db.AuthAccounts.Add(account);
        }
        else
        {
            account.Email ??= email;
            account.LastSignInUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // ExpiresAt shape differs between Gotrue SDK versions; keep it optional.
        DateTimeOffset? expiresAt = null;

        await _tokenStore.SetTokensAsync(account.Id, session.AccessToken, session.RefreshToken, expiresAt);
        await _tokenStore.SetActiveAccountIdAsync(account.Id);
    }
}
