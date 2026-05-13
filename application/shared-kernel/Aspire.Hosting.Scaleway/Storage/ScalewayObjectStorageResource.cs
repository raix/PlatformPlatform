namespace Aspire.Hosting.Scaleway.Storage;

/// <summary>
///     Represents Scaleway Object Storage (S3-compatible).
///     This is not part of the Scaleway management API (uses S3 protocol directly),
///     so it cannot be auto-generated and is hand-written.
/// </summary>
public sealed class ScalewayObjectStorageResource(string name)
    : Resource(name), IScalewayResource, IResourceWithConnectionString
{
    public ScalewayRegion Region { get; init; } = ScalewayRegion.FrPar;

    public string[]? Tags { get; set; }

    public string Endpoint => $"https://s3.{Region.ToApiString()}.scw.cloud";

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Endpoint}");

    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
}
