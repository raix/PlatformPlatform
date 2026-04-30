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
}
