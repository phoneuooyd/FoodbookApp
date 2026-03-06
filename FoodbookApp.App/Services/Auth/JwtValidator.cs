using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FoodbookApp.Services.Auth;

public sealed class JwtValidator : IJwtValidator
{
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtValidator(JwtValidationOptions options)
    {
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer),
            ValidIssuer = string.IsNullOrWhiteSpace(options.Issuer) ? null : options.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
            ValidAudience = string.IsNullOrWhiteSpace(options.Audience) ? null : options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    }

    public ClaimsPrincipal? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            var principal = _handler.ValidateToken(token, _validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
