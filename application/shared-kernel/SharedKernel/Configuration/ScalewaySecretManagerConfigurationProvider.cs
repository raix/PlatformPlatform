using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SharedKernel.Configuration;

/// <summary>
/// .NET configuration provider that loads secrets from Scaleway Secret Manager.
/// Secrets are fetched via the REST API and injected into the configuration system.
/// Supports periodic refresh to pick up secret rotations.
/// </summary>
public sealed class ScalewaySecretManagerConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly ScalewaySecretManagerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Timer? _refreshTimer;

    public ScalewaySecretManagerConfigurationProvider(ScalewaySecretManagerOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? CreateDefaultHttpClient(options);
        if (httpClient is not null && !_httpClient.DefaultRequestHeaders.Contains("X-Auth-Token"))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", options.SecretKey);
        }

        if (options.ReloadInterval is not null)
        {
            _refreshTimer = new Timer(_ => LoadAsync().ConfigureAwait(false), null, options.ReloadInterval.Value, options.ReloadInterval.Value);
        }
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        var secrets = await ListSecretsAsync();
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var secret in secrets)
        {
            var secretId = secret.GetProperty("id").GetString()!;
            var secretName = secret.GetProperty("name").GetString()!;

            var value = await AccessSecretValueAsync(secretId);
            if (value is not null)
            {
                // Convert secret names to configuration keys (e.g., "authentication-token-signing-key" → "authentication-token-signing-key")
                data[secretName] = value;
            }
        }

        Data = data;
        OnReload();
    }

    private async Task<JsonElement[]> ListSecretsAsync()
    {
        var url = $"/secret-manager/v1beta1/regions/{_options.Region}/secrets?project_id={_options.ProjectId}";

        if (_options.Tags is { Length: > 0 })
        {
            foreach (var tag in _options.Tags)
            {
                url += $"&tags={Uri.EscapeDataString(tag)}";
            }
        }

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("secrets", out var secrets))
        {
            return secrets.EnumerateArray().ToArray();
        }

        return [];
    }

    private async Task<string?> AccessSecretValueAsync(string secretId)
    {
        var url = $"/secret-manager/v1beta1/regions/{_options.Region}/secrets/{secretId}/versions/latest_enabled/access";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            var base64 = data.GetString();
            if (base64 is not null)
            {
                var bytes = Convert.FromBase64String(base64);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
        }

        return null;
    }

    private static HttpClient CreateDefaultHttpClient(ScalewaySecretManagerOptions options)
    {
        var client = new HttpClient { BaseAddress = new Uri(options.ApiUrl) };
        client.DefaultRequestHeaders.Add("X-Auth-Token", options.SecretKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        _httpClient.Dispose();
    }
}

public sealed class ScalewaySecretManagerOptions
{
    public string SecretKey { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Region { get; set; } = "fr-par";

    public string ApiUrl { get; set; } = "https://api.scaleway.com";

    public string[]? Tags { get; set; }

    public TimeSpan? ReloadInterval { get; set; }
}

public sealed class ScalewaySecretManagerConfigurationSource(ScalewaySecretManagerOptions options) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ScalewaySecretManagerConfigurationProvider(options);
    }
}

public static class ScalewaySecretManagerConfigurationExtensions
{
    /// <summary>
    /// Adds Scaleway Secret Manager as a configuration source.
    /// Secrets are loaded at startup and optionally refreshed on an interval.
    /// </summary>
    public static IConfigurationBuilder AddScalewaySecretManager(
        this IConfigurationBuilder builder,
        string? secretKey = null,
        string? projectId = null,
        string? region = null,
        string[]? tags = null,
        TimeSpan? reloadInterval = null)
    {
        var options = new ScalewaySecretManagerOptions
        {
            SecretKey = secretKey ?? Environment.GetEnvironmentVariable("SCW_SECRET_KEY") ?? string.Empty,
            ProjectId = projectId ?? Environment.GetEnvironmentVariable("SCW_DEFAULT_PROJECT_ID") ?? string.Empty,
            Region = region ?? Environment.GetEnvironmentVariable("SCW_DEFAULT_REGION") ?? "fr-par",
            Tags = tags,
            ReloadInterval = reloadInterval
        };

        return builder.Add(new ScalewaySecretManagerConfigurationSource(options));
    }
}
