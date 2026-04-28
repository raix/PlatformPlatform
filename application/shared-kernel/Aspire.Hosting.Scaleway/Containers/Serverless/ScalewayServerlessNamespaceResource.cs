namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a Scaleway Serverless Containers namespace.
/// Namespaces group serverless containers together.
/// </summary>
public sealed class ScalewayServerlessNamespaceResource(string name)
    : Resource(name), IScalewayResource
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public string? Description { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public Dictionary<string, string> EnvironmentVariables { get; } = new();
}
