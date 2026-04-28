namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a Scaleway Serverless Container.
/// Serverless Containers run container images with automatic scaling and pay-per-use billing.
/// </summary>
public sealed class ScalewayServerlessContainerResource(string name, ScalewayServerlessNamespaceResource containerNamespace)
    : Resource(name), IScalewayResource, IResourceWithConnectionString, IResourceWithEnvironment,
      IResourceWithParent<ScalewayServerlessNamespaceResource>
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public ScalewayServerlessNamespaceResource Parent { get; } = containerNamespace ?? throw new ArgumentNullException(nameof(containerNamespace));

    public string? RegistryImage { get; set; }

    public int MemoryLimitMb { get; set; } = 256;

    public int CpuLimitMillicores { get; set; } = 140;

    public int MinScale { get; set; }

    public int MaxScale { get; set; } = 20;

    public int MaxConcurrency { get; set; } = 50;

    public int TimeoutSeconds { get; set; } = 300;

    public int Port { get; set; } = 8080;

    public ScalewayContainerPrivacy Privacy { get; set; } = ScalewayContainerPrivacy.Public;

    public string? HealthCheckPath { get; set; }

    public string? Description { get; set; }

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"https://{Name}.functions.fnc.{Parent.Region.ToApiString()}.scw.cloud");
}

public enum ScalewayContainerPrivacy
{
    Public,
    Private
}
