namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Provides lazy-initialized shared Scaleway infrastructure resources.
/// Mirrors the CDKDefaultsProvider pattern from the AWS Aspire package.
/// Each property creates the resource on first access and caches it.
/// </summary>
public sealed class ScalewayDefaultsProvider
{
    private readonly ScalewayEnvironmentResource _environment;

    public ScalewayDefaultsProvider(ScalewayEnvironmentResource environment)
    {
        _environment = environment;
    }

    public string ProjectId => _environment.CredentialConfig.DefaultProjectId ?? string.Empty;

    public ScalewayRegion Region => _environment.CredentialConfig.DefaultRegion;

    /// <summary>
    /// Scaleway Private Network for isolating resources. All managed databases and containers
    /// are attached to this network so they can communicate without public internet exposure.
    /// </summary>
    public ScalewayPrivateNetworkConfig PrivateNetwork => _privateNetwork ??= CreateDefaultPrivateNetwork();
    private ScalewayPrivateNetworkConfig? _privateNetwork;

    /// <summary>
    /// Container Registry namespace where built Docker images are pushed.
    /// </summary>
    public ScalewayRegistryConfig Registry => _registry ??= CreateDefaultRegistry();
    private ScalewayRegistryConfig? _registry;

    /// <summary>
    /// Serverless Container namespace that groups all deployed containers.
    /// </summary>
    public ScalewayContainerNamespaceConfig ContainerNamespace => _containerNamespace ??= CreateDefaultContainerNamespace();
    private ScalewayContainerNamespaceConfig? _containerNamespace;

    private ScalewayPrivateNetworkConfig CreateDefaultPrivateNetwork()
    {
        return new ScalewayPrivateNetworkConfig($"{_environment.Name}-network", Region);
    }

    private ScalewayRegistryConfig CreateDefaultRegistry()
    {
        return new ScalewayRegistryConfig($"{_environment.Name}-registry", Region);
    }

    private ScalewayContainerNamespaceConfig CreateDefaultContainerNamespace()
    {
        return new ScalewayContainerNamespaceConfig($"{_environment.Name}-containers", Region);
    }
}

/// <summary>
/// Configuration for a Scaleway Private Network.
/// </summary>
public sealed record ScalewayPrivateNetworkConfig(string Name, ScalewayRegion Region);

/// <summary>
/// Configuration for a Scaleway Container Registry namespace.
/// </summary>
public sealed record ScalewayRegistryConfig(string Name, ScalewayRegion Region)
{
    public string Endpoint => $"rg.{Region.ToApiString()}.scw.cloud/{Name}";
}

/// <summary>
/// Configuration for a Scaleway Serverless Container namespace.
/// </summary>
public sealed record ScalewayContainerNamespaceConfig(string Name, ScalewayRegion Region);
