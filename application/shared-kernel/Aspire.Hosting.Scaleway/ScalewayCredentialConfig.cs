namespace Aspire.Hosting.Scaleway;

/// <summary>
///     Configuration for authenticating with the Scaleway API.
///     Values are typically sourced from SCW_* environment variables.
/// </summary>
public sealed class ScalewayCredentialConfig
{
    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public string? DefaultProjectId { get; init; }

    public string? DefaultOrganizationId { get; init; }

    public ScalewayRegion DefaultRegion { get; init; } = ScalewayRegion.FrPar;

    public ScalewayZone DefaultZone { get; init; } = ScalewayZone.FrPar1;

    public string ApiUrl { get; init; } = "https://api.scaleway.com";

    /// <summary>
    ///     Builds a <see cref="ScalewayCredentialConfig" /> from <c>SCW_*</c> environment variables,
    ///     with optional explicit overrides for the values most commonly set per-environment.
    /// </summary>
    public static ScalewayCredentialConfig FromEnvironment(
        string? accessKey = null,
        string? secretKey = null,
        string? defaultProjectId = null,
        ScalewayRegion? defaultRegion = null)
    {
        return new ScalewayCredentialConfig
        {
            AccessKey = accessKey ?? Environment.GetEnvironmentVariable("SCW_ACCESS_KEY"),
            SecretKey = secretKey ?? Environment.GetEnvironmentVariable("SCW_SECRET_KEY"),
            DefaultProjectId = defaultProjectId ?? Environment.GetEnvironmentVariable("SCW_DEFAULT_PROJECT_ID"),
            DefaultOrganizationId = Environment.GetEnvironmentVariable("SCW_DEFAULT_ORGANIZATION_ID"),
            DefaultRegion = defaultRegion ?? ParseRegionFromEnvironment(),
            DefaultZone = ParseZoneFromEnvironment(),
            ApiUrl = Environment.GetEnvironmentVariable("SCW_API_URL") ?? "https://api.scaleway.com"
        };
    }

    private static ScalewayRegion ParseRegionFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("SCW_DEFAULT_REGION");
        return value is not null ? ScalewayRegionExtensions.ParseRegion(value) : ScalewayRegion.FrPar;
    }

    private static ScalewayZone ParseZoneFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("SCW_DEFAULT_ZONE");
        return value is not null ? ScalewayZoneExtensions.ParseZone(value) : ScalewayZone.FrPar1;
    }
}
