namespace Aspire.Hosting.Scaleway.Storage;

/// <summary>
///     Represents a bucket within Scaleway Object Storage.
/// </summary>
public sealed class ScalewayBucketResource(string name, string bucketName, ScalewayObjectStorageResource parent)
    : Resource(name), IResourceWithParent<ScalewayObjectStorageResource>, IResourceWithConnectionString
{
    public string BucketName { get; } = bucketName;

    public ScalewayBucketAcl Acl { get; set; } = ScalewayBucketAcl.Private;

    public bool Versioning { get; set; }

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Parent.Endpoint}/{BucketName}");

    public ScalewayObjectStorageResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));
}

public enum ScalewayBucketAcl
{
    Private,
    PublicRead,
    PublicReadWrite
}
