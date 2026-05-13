namespace Aspire.Hosting.Scaleway;

/// <summary>
///     Base resource for Scaleway services that do not expose a connection string or endpoints.
/// </summary>
public sealed class ScalewayResource(string name) : Resource(name), IScalewayResource
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
}
