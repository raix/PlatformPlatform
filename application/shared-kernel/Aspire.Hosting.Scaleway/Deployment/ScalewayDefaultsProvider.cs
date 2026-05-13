namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Provides lazy-initialized shared Scaleway infrastructure resources.
///     Mirrors the CDKDefaultsProvider pattern from the AWS Aspire package.
///     Each property creates the resource on first access and caches it.
/// </summary>
public sealed class ScalewayDefaultsProvider(ScalewayEnvironmentResource environment)
{
    public string ProjectId => environment.CredentialConfig.DefaultProjectId ?? string.Empty;

    public ScalewayRegion Region => environment.CredentialConfig.DefaultRegion;

    /// <summary>
    ///     Scaleway Private Network for isolating resources. All managed databases and containers
    ///     are attached to this network so they can communicate without public internet exposure.
    /// </summary>
    public ScalewayPrivateNetworkConfig PrivateNetwork =>
        field ??= new ScalewayPrivateNetworkConfig($"{environment.Name}-network", Region);

    /// <summary>
    ///     Container Registry namespace where built Docker images are pushed.
    /// </summary>
    public ScalewayRegistryConfig Registry =>
        field ??= new ScalewayRegistryConfig($"{environment.Name}-registry", Region);

    /// <summary>
    ///     Serverless Container namespace that groups all deployed containers.
    /// </summary>
    public ScalewayContainerNamespaceConfig ContainerNamespace =>
        field ??= new ScalewayContainerNamespaceConfig($"{environment.Name}-containers", Region);
}

/// <summary>
///     Configuration for a Scaleway Private Network.
/// </summary>
public sealed record ScalewayPrivateNetworkConfig(string Name, ScalewayRegion Region);

/// <summary>
///     Configuration for a Scaleway Container Registry namespace.
/// </summary>
public sealed record ScalewayRegistryConfig(string Name, ScalewayRegion Region)
{
    public string Endpoint => $"rg.{Region.ToApiString()}.scw.cloud/{Name}";
}

/// <summary>
///     Configuration for a Scaleway Serverless Container namespace.
/// </summary>
public sealed record ScalewayContainerNamespaceConfig(string Name, ScalewayRegion Region);
