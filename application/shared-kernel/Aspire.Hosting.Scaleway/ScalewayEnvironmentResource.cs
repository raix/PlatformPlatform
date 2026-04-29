namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Root deployment target for Scaleway resources, analogous to AWSCDKEnvironmentResource.
/// Groups all Scaleway resources under a common region, project, and shared infrastructure
/// (private network, container registry, container namespace).
/// </summary>
public sealed class ScalewayEnvironmentResource : Resource
{
    public ScalewayEnvironmentResource(string name, ScalewayCredentialConfig credentialConfig, bool isPublishMode)
        : base(name)
    {
        CredentialConfig = credentialConfig;
        IsPublishMode = isPublishMode;
        DefaultsProvider = new ScalewayDefaultsProvider(this);
    }

    public ScalewayCredentialConfig CredentialConfig { get; }

    public bool IsPublishMode { get; }

    /// <summary>
    /// Provides lazy-initialized shared infrastructure (private network, registry, container namespace).
    /// </summary>
    public ScalewayDefaultsProvider DefaultsProvider { get; }
}
