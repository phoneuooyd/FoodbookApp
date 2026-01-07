using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace FoodbookApp.Services.Auth;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IAuthTokenStore _tokenStore;
    private readonly string? _supabaseApiKey;

    public BearerTokenHandler(IAuthTokenStore tokenStore, IConfiguration configuration)
    {
        _tokenStore = tokenStore;
        _supabaseApiKey = configuration["Supabase:Key"]; 
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Supabase REST/PostgREST expects `apikey` header on every request
        if (!string.IsNullOrWhiteSpace(_supabaseApiKey) && !request.Headers.Contains("apikey"))
        {
            request.Headers.TryAddWithoutValidation("apikey", _supabaseApiKey);
        }

        var token = await _tokenStore.GetAccessTokenAsync();

        if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization == null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
