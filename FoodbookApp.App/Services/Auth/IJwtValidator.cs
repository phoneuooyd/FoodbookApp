using System.Security.Claims;

namespace FoodbookApp.Services.Auth;

public interface IJwtValidator
{
    ClaimsPrincipal? Validate(string token);
}
