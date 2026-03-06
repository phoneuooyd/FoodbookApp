using Foodbook.Models;
using FoodbookApp.Models;

namespace FoodbookApp.Interfaces;

public interface IAccountService
{
    Task<IReadOnlyList<AuthAccount>> GetAccountsAsync(CancellationToken ct = default);
    Task<AuthAccount?> GetActiveAccountAsync(CancellationToken ct = default);
    Task<AuthAccount> SignInAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default);
    Task<AuthAccount> SignUpAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default);
    Task SignOutAsync(bool clearAutoLogin, CancellationToken ct = default);
    Task SwitchAccountAsync(Guid accountId, bool enableAutoLogin, CancellationToken ct = default);
    Task<bool> TryAutoLoginAsync(CancellationToken ct = default);
}
