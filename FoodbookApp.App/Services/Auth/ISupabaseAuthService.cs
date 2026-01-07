using Supabase.Gotrue;

namespace FoodbookApp.Services.Auth;

public interface ISupabaseAuthService
{
    Session? CurrentSession { get; }

    Task<Session> SignUpAsync(string email, string password);
    Task<Session> SignInAsync(string email, string password);
    Task SignOutAsync();
}
