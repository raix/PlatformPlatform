using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayStorageExtensions
{
    /// <summary>
    /// Adds Scaleway Object Storage (S3-compatible) to the application model.
    /// </summary>
    public static IResourceBuilder<ScalewayObjectStorageResource> AddScalewayObjectStorage(
        this IDistributedApplicationBuilder builder,
        string name,
        ScalewayRegion? region = null)
    {
        var resource = new ScalewayObjectStorageResource(name)
        {
            Region = region ?? ScalewayRegion.FrPar
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Adds a bucket to Scaleway Object Storage.
    /// </summary>
    public static IResourceBuilder<ScalewayBucketResource> AddBucket(
        this IResourceBuilder<ScalewayObjectStorageResource> builder,
        string name,
        string? bucketName = null)
    {
        var actualBucketName = bucketName ?? name;
        var resource = new ScalewayBucketResource(name, actualBucketName, builder.Resource);
        return builder.ApplicationBuilder.AddResource(resource);
    }

    /// <summary>
    /// Sets the ACL (access control) for a bucket.
    /// </summary>
    public static IResourceBuilder<ScalewayBucketResource> WithAcl(
        this IResourceBuilder<ScalewayBucketResource> builder,
        ScalewayBucketAcl acl)
    {
        builder.Resource.Acl = acl;
        return builder;
    }

    /// <summary>
    /// Enables versioning on a bucket.
    /// </summary>
    public static IResourceBuilder<ScalewayBucketResource> WithVersioning(
        this IResourceBuilder<ScalewayBucketResource> builder)
    {
        builder.Resource.Versioning = true;
        return builder;
    }
}
