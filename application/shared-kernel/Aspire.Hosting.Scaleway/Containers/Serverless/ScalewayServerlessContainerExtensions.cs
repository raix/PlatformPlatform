using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayServerlessContainerExtensions
{
    /// <summary>
    /// Adds a Scaleway Serverless Containers namespace to the application model.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessNamespaceResource> AddScalewayServerlessNamespace(
        this IDistributedApplicationBuilder builder,
        string name,
        ScalewayRegion? region = null)
    {
        var resource = new ScalewayServerlessNamespaceResource(name)
        {
            Region = region ?? ScalewayRegion.FrPar
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Adds a Scaleway Serverless Container to a namespace.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> AddContainer(
        this IResourceBuilder<ScalewayServerlessNamespaceResource> builder,
        string name,
        string? image = null)
    {
        var resource = new ScalewayServerlessContainerResource(name, builder.Resource)
        {
            RegistryImage = image
        };

        return builder.ApplicationBuilder.AddResource(resource);
    }

    /// <summary>
    /// Publishes a .NET project as a Scaleway Serverless Container.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> PublishAsScalewayContainer(
        this IResourceBuilder<ProjectResource> projectBuilder,
        IResourceBuilder<ScalewayServerlessNamespaceResource> namespaceBuilder)
    {
        var container = new ScalewayServerlessContainerResource(
            projectBuilder.Resource.Name,
            namespaceBuilder.Resource
        );

        return projectBuilder.ApplicationBuilder.AddResource(container);
    }

    /// <summary>
    /// Sets the memory limit for the serverless container.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> WithMemory(
        this IResourceBuilder<ScalewayServerlessContainerResource> builder,
        int memoryMb)
    {
        builder.Resource.MemoryLimitMb = memoryMb;
        return builder;
    }

    /// <summary>
    /// Configures autoscaling for the serverless container.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> WithScaling(
        this IResourceBuilder<ScalewayServerlessContainerResource> builder,
        int minScale = 0,
        int maxScale = 20,
        int maxConcurrency = 50)
    {
        builder.Resource.MinScale = minScale;
        builder.Resource.MaxScale = maxScale;
        builder.Resource.MaxConcurrency = maxConcurrency;
        return builder;
    }

    /// <summary>
    /// Sets the container privacy (public or private).
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> WithPrivacy(
        this IResourceBuilder<ScalewayServerlessContainerResource> builder,
        ScalewayContainerPrivacy privacy)
    {
        builder.Resource.Privacy = privacy;
        return builder;
    }

    /// <summary>
    /// Sets a health check path for the serverless container.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> WithHealthCheck(
        this IResourceBuilder<ScalewayServerlessContainerResource> builder,
        string path)
    {
        builder.Resource.HealthCheckPath = path;
        return builder;
    }

    /// <summary>
    /// Sets the request timeout for the serverless container.
    /// </summary>
    public static IResourceBuilder<ScalewayServerlessContainerResource> WithTimeout(
        this IResourceBuilder<ScalewayServerlessContainerResource> builder,
        int timeoutSeconds)
    {
        builder.Resource.TimeoutSeconds = timeoutSeconds;
        return builder;
    }
}
