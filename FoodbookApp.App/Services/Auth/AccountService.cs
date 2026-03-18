using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Foodbook.Data;
using FoodbookApp.Interfaces;
using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using Supabase.Gotrue;

namespace FoodbookApp.Services.Auth;

public sealed class AccountService : IAccountService
{
    private readonly AppDbContext _db;
    private readonly ISupabaseAuthService _auth;
    private readonly IAuthTokenStore _tokenStore;
    private readonly global::Supabase.Client _client;
    private readonly ISupabaseSyncService _syncService;

    public AccountService(
        AppDbContext db,
        ISupabaseAuthService auth,
        IAuthTokenStore tokenStore,
        global::Supabase.Client client,
        ISupabaseSyncService syncService)
    {
        _db = db;
        _auth = auth;
        _tokenStore = tokenStore;
        _client = client;
        _syncService = syncService;
    }

    public async Task<IReadOnlyList<AuthAccount>> GetAccountsAsync(CancellationToken ct = default)
        => await _db.AuthAccounts.AsNoTracking().OrderByDescending(a => a.LastSignInUtc ?? a.CreatedUtc).ToListAsync(ct);

    public async Task<AuthAccount?> GetActiveAccountAsync(CancellationToken ct = default)
    {
        var activeId = await _tokenStore.GetActiveAccountIdAsync();
        if (!activeId.HasValue) return null;
        return await _db.AuthAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == activeId.Value, ct);
    }

    public async Task<AuthAccount> SignInAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default)
    {
        var session = await _auth.SignInAsync(email, password);
        var account = await ResolveAccountFromSessionAsync(session, email, ct);
        await ApplyAutoLoginSelectionAsync(account.Id, enableAutoLogin, ct);
        return account;
    }

    public async Task<AuthAccount> SignUpAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default)
    {
        var session = await _auth.SignUpAsync(email, password);
        var account = await ResolveAccountFromSessionAsync(session, email, ct);
        await ApplyAutoLoginSelectionAsync(account.Id, enableAutoLogin, ct);
        return account;
    }

    public async Task SignOutAsync(bool clearAutoLogin, CancellationToken ct = default)
    {
        var activeId = await _tokenStore.GetActiveAccountIdAsync();

        await _auth.SignOutAsync();

        if (activeId.HasValue)
        {
            var account = await _db.AuthAccounts.FirstOrDefaultAsync(a => a.Id == activeId.Value, ct);
            if (account != null && clearAutoLogin)
            {
                account.IsAutoLoginEnabled = false;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task SwitchAccountAsync(Guid accountId, bool enableAutoLogin, CancellationToken ct = default)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("AccountId is required", nameof(accountId));

        var account = await _db.AuthAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                      ?? throw new InvalidOperationException($"Account {accountId} not found");

        await _tokenStore.SetActiveAccountIdAsync(account.Id);
        await ApplyAutoLoginSelectionAsync(account.Id, enableAutoLogin, ct);

        await EnsureClientSessionForActiveAccountAsync(ct);
    }

    public async Task<bool> TryAutoLoginAsync(CancellationToken ct = default)
    {
        var account = await _db.AuthAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.IsAutoLoginEnabled, ct);
        if (account == null) return false;

        await _tokenStore.SetActiveAccountIdAsync(account.Id);

        var restored = await EnsureClientSessionForActiveAccountAsync(ct);

        if (restored)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountService] TryAutoLoginAsync: Session restored for account {account.Id}");

            try
            {
                // Apply saved cloud sync preference if present
                var storageKey = $"cloudsync.enabled:{account.Id:N}";
                var saved = await SecureStorage.GetAsync(storageKey);
                if (saved == "true")
                {
                    System.Diagnostics.Debug.WriteLine("[AccountService] Applying saved sync preference: enabled");
                    await _syncService.EnableCloudSyncAsync(ct);
                }
                else if (saved == "false")
                {
                    System.Diagnostics.Debug.WriteLine("[AccountService] Applying saved sync preference: disabled");
                    await _syncService.DisableCloudSyncAsync(ct);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[AccountService] No saved sync preference in SecureStorage for this account");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountService] Error applying saved sync preference: {ex.Message}");
            }
        }

        return restored;
    }

    private async Task<AuthAccount> ResolveAccountFromSessionAsync(Session session, string? email, CancellationToken ct)
    {
        var sbUserId = session?.User?.Id ?? _client.Auth.CurrentUser?.Id;
        if (string.IsNullOrWhiteSpace(sbUserId))
            throw new InvalidOperationException("Supabase user id not available after sign-in");

        var account = await _db.AuthAccounts.FirstOrDefaultAsync(a => a.SupabaseUserId == sbUserId, ct);
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

        await _db.SaveChangesAsync(ct);

        return account;
    }

    private async Task ApplyAutoLoginSelectionAsync(Guid activeAccountId, bool enableAutoLogin, CancellationToken ct)
    {
        if (!enableAutoLogin)
        {
            var current = await _db.AuthAccounts.FirstOrDefaultAsync(a => a.Id == activeAccountId, ct);
            if (current != null)
            {
                current.IsAutoLoginEnabled = false;
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        var all = await _db.AuthAccounts.ToListAsync(ct);
        foreach (var acc in all)
        {
            acc.IsAutoLoginEnabled = acc.Id == activeAccountId;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> EnsureClientSessionForActiveAccountAsync(CancellationToken ct)
    {
        var activeId = await _tokenStore.GetActiveAccountIdAsync();
        if (!activeId.HasValue)
        {
            System.Diagnostics.Debug.WriteLine("[AccountService] EnsureClientSession: No active account ID stored");
            return false;
        }

        System.Diagnostics.Debug.WriteLine($"[AccountService] EnsureClientSession: Found active account {activeId.Value}");

        var access = await _tokenStore.GetAccessTokenAsync(activeId.Value);
        var refresh = await _tokenStore.GetRefreshTokenAsync(activeId.Value);
        
        System.Diagnostics.Debug.WriteLine($"[AccountService] EnsureClientSession: access token present={!string.IsNullOrWhiteSpace(access)}, refresh token present={!string.IsNullOrWhiteSpace(refresh)}");

        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh))
        {
            System.Diagnostics.Debug.WriteLine("[AccountService] EnsureClientSession: One or both tokens are missing - cannot restore session");
            return false;
        }

        System.Diagnostics.Debug.WriteLine("[AccountService] EnsureClientSession: Both tokens present - attempting session restore...");

        // Try to restore the session using stored tokens
        try
        {
            System.Diagnostics.Debug.WriteLine("[AccountService] EnsureClientSession: Calling SupabaseAuthService.SetSessionAsync");
            var session = await _auth.SetSessionAsync(access, refresh);
            if (session != null)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountService] EnsureClientSession: Session restored successfully! User: {session.User?.Email}");
                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AccountService] EnsureClientSession: SetSessionAsync returned null");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountService] EnsureClientSession: SetSessionAsync threw exception: {ex.Message}");
        }

        // Fallback: Initialize client and check if session exists
        try
        {
            System.Diagnostics.Debug.WriteLine("[AccountService] EnsureClientSession: Fallback - initializing client...");
            await _client.InitializeAsync();
            var hasSession = _client.Auth.CurrentSession != null;
            System.Diagnostics.Debug.WriteLine($"[AccountService] EnsureClientSession: After init, CurrentSession exists: {hasSession}");
            return hasSession;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountService] EnsureClientSession: Client init failed - {ex.Message}");
            return false;
        }
    }
}
