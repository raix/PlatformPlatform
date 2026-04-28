namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Root deployment target for Scaleway resources, analogous to AzureEnvironmentResource.
/// Groups all Scaleway resources under a common region and project.
/// </summary>
public sealed class ScalewayEnvironmentResource(string name, ScalewayCredentialConfig credentialConfig)
    : Resource(name)
{
    public ScalewayCredentialConfig CredentialConfig { get; } = credentialConfig;
}
