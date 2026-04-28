namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Configuration for authenticating with the Scaleway API.
/// Values are typically sourced from SCW_* environment variables.
/// </summary>
public sealed class ScalewayCredentialConfig
{
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string? DefaultProjectId { get; set; }

    public string? DefaultOrganizationId { get; set; }

    public ScalewayRegion DefaultRegion { get; set; } = ScalewayRegion.FrPar;

    public ScalewayZone DefaultZone { get; set; } = ScalewayZone.FrPar1;

    public string ApiUrl { get; set; } = "https://api.scaleway.com";
}
