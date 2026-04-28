namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Resource that holds Scaleway credential configuration for use by other resources and projects.
/// </summary>
public sealed class ScalewayCredentialConfigResource(string name, ScalewayCredentialConfig config)
    : Resource(name)
{
    public ScalewayCredentialConfig Config { get; } = config;
}
