using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayRegistryExtensions
{
    /// <summary>
    /// Adds a Scaleway Container Registry namespace to the application model.
    /// </summary>
    public static IResourceBuilder<ScalewayRegistryNamespaceResource> AddScalewayRegistry(
        this IDistributedApplicationBuilder builder,
        string name,
        ScalewayRegion? region = null)
    {
        var resource = new ScalewayRegistryNamespaceResource(name)
        {
            Region = region ?? ScalewayRegion.FrPar
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Makes the container registry namespace publicly accessible.
    /// </summary>
    public static IResourceBuilder<ScalewayRegistryNamespaceResource> WithPublicAccess(
        this IResourceBuilder<ScalewayRegistryNamespaceResource> builder)
    {
        builder.Resource.IsPublic = true;
        return builder;
    }

    /// <summary>
    /// Sets a description for the registry namespace.
    /// </summary>
    public static IResourceBuilder<ScalewayRegistryNamespaceResource> WithDescription(
        this IResourceBuilder<ScalewayRegistryNamespaceResource> builder,
        string description)
    {
        builder.Resource.Description = description;
        return builder;
    }
}
