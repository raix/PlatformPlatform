namespace Aspire.Hosting.Scaleway;

public static class ScalewayCredentialExtensions
{
    /// <summary>
    ///     Adds Scaleway credential configuration to the distributed application builder.
    ///     Reads from SCW_* environment variables and optional configuration overrides.
    /// </summary>
    public static IResourceBuilder<ScalewayCredentialConfigResource> AddScalewayCredentialConfig(
        this IDistributedApplicationBuilder builder,
        string? accessKey = null,
        string? secretKey = null,
        string? defaultProjectId = null,
        ScalewayRegion? defaultRegion = null)
    {
        var config = new ScalewayCredentialConfig
        {
            AccessKey = accessKey ?? Environment.GetEnvironmentVariable("SCW_ACCESS_KEY"),
            SecretKey = secretKey ?? Environment.GetEnvironmentVariable("SCW_SECRET_KEY"),
            DefaultProjectId = defaultProjectId ?? Environment.GetEnvironmentVariable("SCW_DEFAULT_PROJECT_ID"),
            DefaultOrganizationId = Environment.GetEnvironmentVariable("SCW_DEFAULT_ORGANIZATION_ID"),
            DefaultRegion = defaultRegion ?? ParseRegionFromEnvironment(),
            DefaultZone = ParseZoneFromEnvironment(),
            ApiUrl = Environment.GetEnvironmentVariable("SCW_API_URL") ?? "https://api.scaleway.com"
        };

        var resource = new ScalewayCredentialConfigResource("scaleway-credentials", config);
        return builder.AddResource(resource);
    }

    /// <summary>
    ///     Injects Scaleway credential environment variables (SCW_ACCESS_KEY, SCW_SECRET_KEY, etc.)
    ///     into a project resource so it can authenticate with Scaleway APIs at runtime.
    /// </summary>
    public static IResourceBuilder<T> WithScalewayCredentials<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ScalewayCredentialConfigResource> credentialBuilder) where T : IResourceWithEnvironment
    {
        var config = credentialBuilder.Resource.Config;

        if (config.AccessKey is not null)
        {
            builder.WithEnvironment("SCW_ACCESS_KEY", config.AccessKey);
        }

        if (config.SecretKey is not null)
        {
            builder.WithEnvironment("SCW_SECRET_KEY", config.SecretKey);
        }

        if (config.DefaultProjectId is not null)
        {
            builder.WithEnvironment("SCW_DEFAULT_PROJECT_ID", config.DefaultProjectId);
        }

        if (config.DefaultOrganizationId is not null)
        {
            builder.WithEnvironment("SCW_DEFAULT_ORGANIZATION_ID", config.DefaultOrganizationId);
        }

        builder.WithEnvironment("SCW_DEFAULT_REGION", config.DefaultRegion.ToApiString());
        builder.WithEnvironment("SCW_DEFAULT_ZONE", config.DefaultZone.ToApiString());

        return builder;
    }

    private static ScalewayRegion ParseRegionFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("SCW_DEFAULT_REGION");
        return value is not null ? ScalewayRegionExtensions.ParseRegion(value) : ScalewayRegion.FrPar;
    }

    private static ScalewayZone ParseZoneFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("SCW_DEFAULT_ZONE");
        return value is not null ? ScalewayZoneExtensions.ParseZone(value) : ScalewayZone.FrPar1;
    }
}
