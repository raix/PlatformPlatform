using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayEnvironmentExtensions
{
    /// <summary>
    /// Adds a Scaleway deployment environment to the distributed application.
    /// This is the root resource that owns shared infrastructure: private network, container registry,
    /// and container namespace. Mirrors the AWSCDKEnvironmentResource pattern.
    /// </summary>
    public static IResourceBuilder<ScalewayEnvironmentResource> AddScalewayEnvironment(
        this IDistributedApplicationBuilder builder,
        string name,
        ScalewayRegion? region = null,
        string? projectId = null)
    {
        var config = new ScalewayCredentialConfig
        {
            AccessKey = Environment.GetEnvironmentVariable("SCW_ACCESS_KEY"),
            SecretKey = Environment.GetEnvironmentVariable("SCW_SECRET_KEY"),
            DefaultProjectId = projectId ?? Environment.GetEnvironmentVariable("SCW_DEFAULT_PROJECT_ID"),
            DefaultOrganizationId = Environment.GetEnvironmentVariable("SCW_DEFAULT_ORGANIZATION_ID"),
            DefaultRegion = region ?? ScalewayRegion.FrPar,
            ApiUrl = Environment.GetEnvironmentVariable("SCW_API_URL") ?? "https://api.scaleway.com"
        };

        var isPublishMode = builder.ExecutionContext.IsPublishMode;
        var resource = new ScalewayEnvironmentResource(name, config, isPublishMode);
        return builder.AddResource(resource);
    }
}
