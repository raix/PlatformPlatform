using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayTemExtensions
{
    /// <summary>
    /// Adds a Scaleway Transactional Email (TEM) domain to the application model.
    /// </summary>
    public static IResourceBuilder<ScalewayTemDomainResource> AddScalewayTransactionalEmail(
        this IDistributedApplicationBuilder builder,
        string name,
        string domainName)
    {
        var resource = new ScalewayTemDomainResource(name)
        {
            DomainName = domainName,
            AcceptTos = true
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Sets the SMTP port for the TEM domain.
    /// </summary>
    public static IResourceBuilder<ScalewayTemDomainResource> WithSmtpPort(
        this IResourceBuilder<ScalewayTemDomainResource> builder,
        int port)
    {
        builder.Resource.SmtpPort = port;
        return builder;
    }
}
