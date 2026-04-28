using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.Authentication.TokenSigning;

/// <summary>
/// Token signing client that reads the signing key, issuer, and audience from environment variables.
/// Used in cloud environments (Scaleway, Kubernetes) where secrets are injected as env vars.
/// </summary>
public sealed class EnvironmentTokenSigningClient : ITokenSigningClient
{
    private readonly byte[] _key;

    public EnvironmentTokenSigningClient()
    {
        var base64Key = Environment.GetEnvironmentVariable("AUTHENTICATION_TOKEN_SIGNING_KEY")
            ?? throw new InvalidOperationException("AUTHENTICATION_TOKEN_SIGNING_KEY environment variable is not configured.");
        _key = Convert.FromBase64String(base64Key);
    }

    public string Issuer =>
        Environment.GetEnvironmentVariable("AUTHENTICATION_TOKEN_ISSUER")
        ?? throw new InvalidOperationException("AUTHENTICATION_TOKEN_ISSUER environment variable is not configured.");

    public string Audience =>
        Environment.GetEnvironmentVariable("AUTHENTICATION_TOKEN_AUDIENCE")
        ?? throw new InvalidOperationException("AUTHENTICATION_TOKEN_AUDIENCE environment variable is not configured.");

    public SigningCredentials GetSigningCredentials()
    {
        var key = new SymmetricSecurityKey(_key);
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha512);
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
}
