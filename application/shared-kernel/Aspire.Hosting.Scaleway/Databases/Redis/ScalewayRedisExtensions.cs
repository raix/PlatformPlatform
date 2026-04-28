using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayRedisExtensions
{
    /// <summary>
    /// Adds a Scaleway Managed Redis cluster to the application model.
    /// </summary>
    public static IResourceBuilder<ScalewayRedisClusterResource> AddScalewayRedisCluster(
        this IDistributedApplicationBuilder builder,
        string name,
        string version = "7.0",
        string nodeType = "RED1-MICRO",
        IResourceBuilder<ParameterResource>? password = null)
    {
        var passwordParameter = password?.Resource
            ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        var resource = new ScalewayRedisClusterResource(name, passwordParameter)
        {
            Version = version,
            NodeType = nodeType
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Sets the availability zone for the Redis cluster.
    /// </summary>
    public static IResourceBuilder<ScalewayRedisClusterResource> WithZone(
        this IResourceBuilder<ScalewayRedisClusterResource> builder,
        ScalewayZone zone)
    {
        builder.Resource.Zone = zone;
        return builder;
    }

    /// <summary>
    /// Sets the cluster size (number of nodes) for the Redis cluster.
    /// </summary>
    public static IResourceBuilder<ScalewayRedisClusterResource> WithClusterSize(
        this IResourceBuilder<ScalewayRedisClusterResource> builder,
        int size)
    {
        builder.Resource.ClusterSize = size;
        return builder;
    }

    /// <summary>
    /// Enables Redis cluster mode.
    /// </summary>
    public static IResourceBuilder<ScalewayRedisClusterResource> WithClusterEnabled(
        this IResourceBuilder<ScalewayRedisClusterResource> builder)
    {
        builder.Resource.ClusterEnabled = true;
        return builder;
    }
}
