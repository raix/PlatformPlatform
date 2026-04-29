using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Typed HTTP client for the Scaleway REST API.
/// Handles authentication, region routing, and JSON serialization.
/// </summary>
public sealed class ScalewayApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ScalewayCredentialConfig _credentials;

    public ScalewayApiClient(ScalewayCredentialConfig credentials)
    {
        _credentials = credentials;
        _httpClient = new HttpClient { BaseAddress = new Uri(credentials.ApiUrl) };
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", credentials.SecretKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    internal ScalewayApiClient(HttpClient httpClient)
    {
        _credentials = new ScalewayCredentialConfig();
        _httpClient = httpClient;
    }

    /// <summary>
    /// Lists resources of a given type, optionally filtered by tags.
    /// </summary>
    public async Task<JsonElement[]> ListResourcesAsync(string apiPath, string region, Dictionary<string, string>? queryParams = null, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(apiPath, region, queryParams);
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        // Scaleway list APIs return objects with a single array property
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                return property.Value.EnumerateArray().ToArray();
            }
        }

        return [];
    }

    /// <summary>
    /// Creates a resource via POST.
    /// </summary>
    public async Task<JsonElement> CreateResourceAsync(string apiPath, string region, object body, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(apiPath, region);
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Updates a resource via PATCH.
    /// </summary>
    public async Task<JsonElement> UpdateResourceAsync(string apiPath, string region, string resourceId, object body, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"{apiPath}/{resourceId}", region);
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Gets a single resource by ID.
    /// </summary>
    public async Task<JsonElement?> GetResourceAsync(string apiPath, string region, string resourceId, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"{apiPath}/{resourceId}", region);
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Deletes a resource by ID.
    /// </summary>
    public async Task DeleteResourceAsync(string apiPath, string region, string resourceId, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"{apiPath}/{resourceId}", region);
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private string BuildUrl(string apiPath, string region, Dictionary<string, string>? queryParams = null)
    {
        var url = $"/{apiPath.TrimStart('/')}".Replace("{region}", region);

        if (queryParams is { Count: > 0 })
        {
            var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            url = $"{url}?{query}";
        }

        return url;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
