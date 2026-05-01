using Aspire.Hosting.Scaleway.Provisioning;

namespace AppHost;

/// <summary>
///     PlatformPlatform-specific defaults for projects published as Scaleway Serverless Containers.
///     Lives in the AppHost (not the reusable Aspire.Hosting.Scaleway library) because the values
///     are policy choices for this app — 512 MB / always-on min scale — not a general default.
/// </summary>
public static class ScalewayContainerExtensions
{
    public static IResourceBuilder<ProjectResource> PublishAsStandardScalewayContainer(
        this IResourceBuilder<ProjectResource> builder,
        int maxScale)
    {
        return builder.PublishAsScalewayContainer(c =>
            {
                c.MemoryLimitMb = 512;
                c.MinScale = 1;
                c.MaxScale = maxScale;
            }
        );
    }
}
