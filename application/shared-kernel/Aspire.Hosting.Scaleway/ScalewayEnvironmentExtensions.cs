using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayEnvironmentExtensions
{
    /// <summary>
    /// Adds a Scaleway deployment environment to the distributed application.
    /// This serves as the root deployment target for all Scaleway resources.
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

        var resource = new ScalewayEnvironmentResource(name, config);
        return builder.AddResource(resource);
    }
}
