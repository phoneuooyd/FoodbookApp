using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FoodbookApp.Services.Auth;

namespace FoodbookApp.Services.Supabase;

/// <summary>
/// REST client for direct Supabase PostgREST API operations with proper authorization headers.
/// Use for operations requiring fine-grained control over HTTP requests.
/// </summary>
public sealed class SupabaseRestClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthTokenStore _tokenStore;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SupabaseRestClient(HttpClient httpClient, IAuthTokenStore tokenStore, string supabaseUrl, string supabaseKey)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _supabaseUrl = supabaseUrl.TrimEnd('/');
        _supabaseKey = supabaseKey;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string endpoint)
    {
        var request = new HttpRequestMessage(method, $"{_supabaseUrl}/rest/v1/{endpoint}");
        
        request.Headers.TryAddWithoutValidation("apikey", _supabaseKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var activeAccountId = await _tokenStore.GetActiveAccountIdAsync();
        if (activeAccountId.HasValue)
        {
            var token = await _tokenStore.GetAccessTokenAsync(activeAccountId.Value);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return request;
    }

    private static string RemoveMetadataFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try
        {
            using var doc = JsonDocument.Parse(json);
            using var ms = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var name = prop.Name;
                        string normalized = name.Replace("_", string.Empty).ToLowerInvariant();
                        if (normalized == "primarykey" || normalized == "tablename")
                            continue;

                        prop.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    writer.WriteStartArray();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            writer.WriteStartObject();
                            foreach (var prop in item.EnumerateObject())
                            {
                                var name = prop.Name;
                                string normalized = name.Replace("_", string.Empty).ToLowerInvariant();
                                if (normalized == "primarykey" || normalized == "tablename")
                                    continue;

                                prop.WriteTo(writer);
                            }
                            writer.WriteEndObject();
                        }
                        else
                        {
                            item.WriteTo(writer);
                        }
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    // Not object/array - return original
                    return json;
                }
                writer.Flush();
            }

            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return json;
        }
    }

    private static string ResolveTable(string table) => SupabaseTableResolver.Resolve(table);

    /// <summary>
    /// GET records from a table with optional filters
    /// </summary>
    public async Task<List<T>> GetAsync<T>(string table, string? filter = null, CancellationToken ct = default)
    {
        table = ResolveTable(table);
        var endpoint = string.IsNullOrEmpty(filter) ? $"{table}?select=*" : $"{table}?select=*&{filter}";
        var request = await CreateRequestAsync(HttpMethod.Get, endpoint);

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
    }

    /// <summary>
    /// GET a single record by ID
    /// </summary>
    public async Task<T?> GetByIdAsync<T>(string table, Guid id, CancellationToken ct = default) where T : class
    {
        table = ResolveTable(table);
        var request = await CreateRequestAsync(HttpMethod.Get, $"{table}?id=eq.{id}&select=*");
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.pgrst.object+json");

        var response = await _httpClient.SendAsync(request, ct);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
            
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>
    /// POST (INSERT) a single record
    /// </summary>
    public async Task<T> InsertAsync<T>(string table, T data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        table = ResolveTable(table);

        var request = await CreateRequestAsync(HttpMethod.Post, table);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        
        var json = JsonSerializer.Serialize(data, JsonOptions);
        json = RemoveMetadataFromJson(json);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<List<T>>(responseJson, JsonOptions);
        
        if (result is { Count: > 0 })
            return result[0];
            
        return data;
    }

    /// <summary>
    /// POST (INSERT) multiple records in batch
    /// </summary>
    public async Task<List<T>> InsertBatchAsync<T>(string table, IEnumerable<T> data, CancellationToken ct = default)
    {
        table = ResolveTable(table);
        var request = await CreateRequestAsync(HttpMethod.Post, table);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        
        var json = JsonSerializer.Serialize(data, JsonOptions);
        json = RemoveMetadataFromJson(json);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<T>>(responseJson, JsonOptions) ?? new List<T>();
    }

    /// <summary>
    /// PATCH (UPDATE) a record by ID
    /// </summary>
    public async Task<T?> UpdateAsync<T>(string table, Guid id, T data, CancellationToken ct = default) where T : class
    {
        table = ResolveTable(table);
        var request = await CreateRequestAsync(new HttpMethod("PATCH"), $"{table}?id=eq.{id}");
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        
        var json = JsonSerializer.Serialize(data, JsonOptions);
        json = RemoveMetadataFromJson(json);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<List<T>>(responseJson, JsonOptions);
        return result?.FirstOrDefault();
    }

    /// <summary>
    /// DELETE a record by ID (hard delete)
    /// </summary>
    public async Task DeleteAsync(string table, Guid id, CancellationToken ct = default)
    {
        table = ResolveTable(table);
        var request = await CreateRequestAsync(HttpMethod.Delete, $"{table}?id=eq.{id}");
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// UPSERT (INSERT or UPDATE on conflict)
    /// </summary>
    public async Task<List<T>> UpsertAsync<T>(string table, IEnumerable<T> data, CancellationToken ct = default)
    {
        table = ResolveTable(table);
        var request = await CreateRequestAsync(HttpMethod.Post, table);
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=representation");
        
        var json = JsonSerializer.Serialize(data, JsonOptions);
        json = RemoveMetadataFromJson(json);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<T>>(responseJson, JsonOptions) ?? new List<T>();
    }

    /// <summary>
    /// Soft delete - sets is_deleted=true instead of hard delete
    /// </summary>
    public async Task SoftDeleteAsync(string table, Guid id, CancellationToken ct = default)
    {
        table = ResolveTable(table);
        var patchData = new { is_deleted = true, deleted_at = DateTime.UtcNow, updated_at = DateTime.UtcNow };
        
        var request = await CreateRequestAsync(new HttpMethod("PATCH"), $"{table}?id=eq.{id}");
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        
        var json = JsonSerializer.Serialize(patchData, JsonOptions);
        json = RemoveMetadataFromJson(json);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var errorContent = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;

        Exception exception = statusCode switch
        {
            401 => new UnauthorizedAccessException($"Supabase: Unauthorized - {errorContent}"),
            403 => new UnauthorizedAccessException($"Supabase: Forbidden (RLS policy) - {errorContent}"),
            404 => new KeyNotFoundException($"Supabase: Resource not found - {errorContent}"),
            409 => new InvalidOperationException($"Supabase: Conflict (duplicate key) - {errorContent}"),
            _ => new HttpRequestException($"Supabase API error {statusCode}: {errorContent}")
        };

        System.Diagnostics.Debug.WriteLine($"[SupabaseRestClient] Error {statusCode}: {errorContent}");
        throw exception;
    }
}
