using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayTemLocalExtensions
{
    /// <summary>
    /// Configures the Scaleway TEM domain to run as a local Mailpit container during development.
    /// Mailpit captures all outgoing emails and provides a web UI for viewing them.
    /// In publish mode, the resource remains a Scaleway cloud resource.
    /// </summary>
    public static IResourceBuilder<ScalewayTemDomainResource> RunAsMailpitContainer(
        this IResourceBuilder<ScalewayTemDomainResource> builder,
        int? httpPort = null,
        int? smtpPort = null,
        Action<IResourceBuilder<ContainerResource>>? configureContainer = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        var containerBuilder = builder.ApplicationBuilder
            .AddContainer($"{builder.Resource.Name}-mailpit", "axllent/mailpit")
            .WithHttpEndpoint(port: httpPort, targetPort: 8025, name: "http")
            .WithEndpoint(port: smtpPort, targetPort: 1025, name: "smtp")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithUrlForEndpoint("http", u => u.DisplayText = "Read mail here");

        configureContainer?.Invoke(containerBuilder);

        return builder;
    }
}
