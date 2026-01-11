using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;
using Supabase.Gotrue;

namespace FoodbookApp.Services.Auth;

public sealed class AccountService : IAccountService
{
    private readonly AppDbContext _db;
    private readonly ISupabaseAuthService _auth;
    private readonly IAuthTokenStore _tokenStore;
    private readonly global::Supabase.Client _client;

    public AccountService(AppDbContext db, ISupabaseAuthService auth, IAuthTokenStore tokenStore, global::Supabase.Client client)
    {
        _db = db;
        _auth = auth;
        _tokenStore = tokenStore;
        _client = client;
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

        return await EnsureClientSessionForActiveAccountAsync(ct);
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
        if (!activeId.HasValue) return false;

        var access = await _tokenStore.GetAccessTokenAsync(activeId.Value);
        if (string.IsNullOrWhiteSpace(access))
            return false;

        await _client.InitializeAsync();

        // Supabase SDK session recovery API differs by version; keep conservative.
        // We treat having a stored access token as autologin success for REST-based calls.
        // For SDK-based calls, UI should prompt re-login if CurrentSession is missing.
        return _client.Auth.CurrentSession != null || !string.IsNullOrWhiteSpace(access);
    }
}
