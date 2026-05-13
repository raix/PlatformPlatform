using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.Authentication.TokenSigning;

/// <summary>
///     Token signing client that reads the signing key, issuer, and audience from Scaleway Secret Manager.
///     Mirrors the AzureTokenSigningClient pattern but uses Scaleway's REST API instead of Key Vault.
/// </summary>
public sealed class ScalewayTokenSigningClient : ITokenSigningClient
{
    private readonly byte[] _key;

    public ScalewayTokenSigningClient()
    {
        using var httpClient = CreateDefaultHttpClient();
        var region = Environment.GetEnvironmentVariable("SCW_DEFAULT_REGION") ?? "fr-par";
        var projectId = Environment.GetEnvironmentVariable("SCW_DEFAULT_PROJECT_ID")!;
        _key = Convert.FromBase64String(GetSecretByName(httpClient, region, projectId, "authentication-token-signing-key"));
        Issuer = GetSecretByName(httpClient, region, projectId, "authentication-token-issuer");
        Audience = GetSecretByName(httpClient, region, projectId, "authentication-token-audience");
    }

    internal ScalewayTokenSigningClient(HttpClient httpClient, string region, string projectId)
    {
        _key = Convert.FromBase64String(GetSecretByName(httpClient, region, projectId, "authentication-token-signing-key"));
        Issuer = GetSecretByName(httpClient, region, projectId, "authentication-token-issuer");
        Audience = GetSecretByName(httpClient, region, projectId, "authentication-token-audience");
    }

    public string Issuer { get; }

    public string Audience { get; }

    public SigningCredentials GetSigningCredentials()
    {
        return new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha512);
    }

    public TokenValidationParameters GetTokenValidationParameters(TimeSpan clockSkew, bool validateLifetime)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_key),
            ClockSkew = clockSkew,
            ValidateLifetime = validateLifetime
        };
    }

    private static string GetSecretByName(HttpClient httpClient, string region, string projectId, string secretName)
    {
        var url = $"/secret-manager/v1beta1/regions/{region}/secrets-by-path/versions/latest_enabled/access?project_id={projectId}&secret_name={secretName}&secret_path=/";
        var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        var base64 = doc.RootElement.GetProperty("data").GetString()!;
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var apiUrl = Environment.GetEnvironmentVariable("SCW_API_URL") ?? "https://api.scaleway.com";
        var secretKey = Environment.GetEnvironmentVariable("SCW_SECRET_KEY")!;
        var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
        client.DefaultRequestHeaders.Add("X-Auth-Token", secretKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
