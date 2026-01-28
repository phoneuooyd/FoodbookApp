using Supabase.Gotrue;

namespace FoodbookApp.Services.Auth;

public interface ISupabaseAuthService
{
    Session? CurrentSession { get; }

    Task<Session> SignUpAsync(string email, string password);
    Task<Session> SignInAsync(string email, string password);
    Task SignOutAsync();
    
    /// <summary>
    /// Restores a session from saved access and refresh tokens.
    /// Used for auto-login on app restart.
    /// </summary>
    Task<Session?> SetSessionAsync(string accessToken, string refreshToken);
}
