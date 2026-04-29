using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayCustomDomainExtensions
{
    /// <summary>
    /// Configures a custom domain for a Scaleway Serverless Container.
    /// At publish time, this creates DNS records and SSL certificates via the Scaleway API.
    /// Locally, it sets the PUBLIC_URL environment variable to the custom domain for URL generation.
    /// </summary>
    public static IResourceBuilder<T> WithCustomDomain<T>(
        this IResourceBuilder<T> builder,
        string domain,
        ScalewayRegion? region = null) where T : IResourceWithEnvironment
    {
        builder.WithAnnotation(new ScalewayCustomDomainAnnotation(domain, region ?? ScalewayRegion.FrPar));
        builder.WithEnvironment("PUBLIC_URL", $"https://{domain}");

        return builder;
    }
}

/// <summary>
/// Annotation that marks a resource for custom domain configuration during deployment.
/// The provisioner reads this to create DNS records and SSL certificates.
/// </summary>
public sealed class ScalewayCustomDomainAnnotation(string domain, ScalewayRegion region) : IResourceAnnotation
{
    public string Domain { get; } = domain;

    public ScalewayRegion Region { get; } = region;
}
