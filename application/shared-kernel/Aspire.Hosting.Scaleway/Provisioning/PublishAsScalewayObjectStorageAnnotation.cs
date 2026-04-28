namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Annotation that marks a resource for provisioning as a Scaleway Object Storage bucket.
/// </summary>
public sealed class PublishAsScalewayObjectStorageAnnotation : IScalewayPublishTargetAnnotation
{
    public ScalewayObjectStoragePublishConfig Config { get; set; } = new();
}

/// <summary>
/// Configuration for publishing Scaleway Object Storage buckets.
/// </summary>
public sealed class ScalewayObjectStoragePublishConfig
{
    public ScalewayBucketAcl Acl { get; set; } = ScalewayBucketAcl.Private;

    public bool Versioning { get; set; }

    public string[]? Tags { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;
}
