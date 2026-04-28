namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents Scaleway Object Storage (S3-compatible).
/// Provides an S3-compatible endpoint for storing blobs.
/// </summary>
public sealed class ScalewayObjectStorageResource(string name)
    : Resource(name), IScalewayResource, IResourceWithConnectionString
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public string[]? Tags { get; set; }

    public string Endpoint => $"https://s3.{Region.ToApiString()}.scw.cloud";

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Endpoint}");
}
