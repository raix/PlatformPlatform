using Aspire.Hosting.Scaleway.Provisioning;

namespace AppHost;

/// <summary>
///     PlatformPlatform-specific helper for projects published as Scaleway Serverless Containers.
///     Reads sizing from <see cref="ContainerProfile" /> so values vary per environment.
/// </summary>
public static class ScalewayContainerExtensions
{
    public static IResourceBuilder<ProjectResource> PublishAsStandardScalewayContainer(
        this IResourceBuilder<ProjectResource> builder,
        ContainerProfile profile)
    {
        return builder.PublishAsScalewayContainer(c =>
            {
                c.MemoryLimitMb = profile.MemoryLimitMb;
                c.MinScale = profile.MinScale;
                c.MaxScale = profile.MaxScale;
            }
        );
    }
}
