using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace FoodbookApp.Services;

/// <summary>
/// Serwis integruj¹cy siê z API OpenAI umo¿liwiaj¹cy uzyskanie odpowiedzi od modelu GPT.
/// </summary>
public class AIService : IAIService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIService> _logger;
    private bool _disposed;

    private const string ApiKeyStorageKey = "OpenAI:ApiKey";
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";
    private const string DefaultModel = "gpt-4o-mini"; // Note: W przyk³adowym kodzie by³o "gpt-4.1-nano" co nie istnieje, wiec damy prawidlowy.

    private Task<string>? _apiKeyTask;

    public AIService(HttpClient httpClient, ILogger<AIService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> GetAIResponseAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt nie mo¿e byæ pusty.", nameof(systemPrompt));
        
        if (string.IsNullOrWhiteSpace(userPrompt))
            throw new ArgumentException("User prompt nie mo¿e byæ pusty.", nameof(userPrompt));

        var apiKey = await GetApiKeyAsync();

        try
        {
            var requestBody = new
            {
                model = DefaultModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            using var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = requestContent
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                return message ?? string.Empty;
            }

            _logger.LogWarning("Otrzymano nieprawid³ow¹ odpowiedŸ od OpenAI API: {Response}", responseContent);
            return string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Wyst¹pi³ b³¹d podczas komunikacji z API OpenAI.");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Wyst¹pi³ b³¹d podczas przetwarzania odpowiedzi formatu JSON z API OpenAI.");
            throw;
        }
    }

    private Task<string> GetApiKeyAsync()
        => _apiKeyTask ??= ResolveApiKeyAsync();

    private static async Task<string> ResolveApiKeyAsync()
    {
        try
        {
            var key = await SecureStorage.GetAsync(ApiKeyStorageKey);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException($"Nie znaleziono klucza OpenAI w SecureStorage (klucz: {ApiKeyStorageKey}).");

            return key;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Nie uda³o siê odczytaæ klucza OpenAI ze SecureStorage.", ex);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Poniewa¿ IHttpClientFactory zazwyczaj zarz¹dza HttpClient, 
                // jawne zwolnienie klienta jest poprawne, 
                // ale dla prostego Singletona/Transienta te¿ siê przyda.
                _httpClient?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
