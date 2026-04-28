using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayPublishExtensions
{
    /// <summary>
    /// Configures a Scaleway RDB instance resource for cloud deployment.
    /// The callback allows platform teams to customize engine, node type, HA, and other settings per environment.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> PublishAsScalewayRdb(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        ScalewayPublishCallback<ScalewayRdbPublishConfig>? configure = null)
    {
        var annotation = new PublishAsScalewayRdbAnnotation();
        configure?.Invoke(annotation.Config);
        builder.WithAnnotation(annotation);
        return builder;
    }

    /// <summary>
    /// Configures a Scaleway Redis cluster resource for cloud deployment.
    /// </summary>
    public static IResourceBuilder<ScalewayRedisClusterResource> PublishAsScalewayRedis(
        this IResourceBuilder<ScalewayRedisClusterResource> builder,
        ScalewayPublishCallback<ScalewayRedisPublishConfig>? configure = null)
    {
        var annotation = new PublishAsScalewayRedisAnnotation();
        configure?.Invoke(annotation.Config);
        builder.WithAnnotation(annotation);
        return builder;
    }

    /// <summary>
    /// Configures a .NET project for deployment as a Scaleway Serverless Container.
    /// </summary>
    public static IResourceBuilder<ProjectResource> PublishAsScalewayContainer(
        this IResourceBuilder<ProjectResource> builder,
        ScalewayPublishCallback<ScalewayContainerPublishConfig>? configure = null)
    {
        var annotation = new PublishAsScalewayContainerAnnotation();
        configure?.Invoke(annotation.Config);
        builder.WithAnnotation(annotation);
        return builder;
    }

    /// <summary>
    /// Configures Scaleway Object Storage for cloud deployment.
    /// </summary>
    public static IResourceBuilder<ScalewayObjectStorageResource> PublishAsScalewayObjectStorage(
        this IResourceBuilder<ScalewayObjectStorageResource> builder,
        ScalewayPublishCallback<ScalewayObjectStoragePublishConfig>? configure = null)
    {
        var annotation = new PublishAsScalewayObjectStorageAnnotation();
        configure?.Invoke(annotation.Config);
        builder.WithAnnotation(annotation);
        return builder;
    }
}
