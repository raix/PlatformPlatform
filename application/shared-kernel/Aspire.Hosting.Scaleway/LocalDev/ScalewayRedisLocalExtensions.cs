using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayRedisLocalExtensions
{
    /// <summary>
    /// Configures the Scaleway Redis cluster to run as a local Redis container during development.
    /// In publish mode, the resource remains a Scaleway cloud resource.
    /// </summary>
    public static IResourceBuilder<ScalewayRedisClusterResource> RunAsRedisContainer(
        this IResourceBuilder<ScalewayRedisClusterResource> builder,
        Action<IResourceBuilder<RedisResource>>? configureContainer = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        var containerBuilder = builder.ApplicationBuilder
            .AddRedis($"{builder.Resource.Name}-container");

        configureContainer?.Invoke(containerBuilder);

        builder.WithAnnotation(new ConnectionStringRedirectAnnotation(containerBuilder.Resource));

        return builder;
    }
}
