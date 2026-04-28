namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a Scaleway Container Registry namespace.
/// Container images are pushed to: rg.{region}.scw.cloud/{namespace}/{image}:{tag}
/// </summary>
public sealed class ScalewayRegistryNamespaceResource(string name)
    : Resource(name), IScalewayResource, IResourceWithConnectionString
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public string Endpoint => $"rg.{Region.ToApiString()}.scw.cloud/{Name}";

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Endpoint}");
}
