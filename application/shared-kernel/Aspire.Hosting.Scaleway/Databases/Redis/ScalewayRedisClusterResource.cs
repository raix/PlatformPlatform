namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a Scaleway Managed Redis cluster.
/// </summary>
public sealed class ScalewayRedisClusterResource(string name, ParameterResource passwordParameter)
    : Resource(name), IScalewayResource, IResourceWithConnectionString, IResourceWithEndpoints
{
    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public string Version { get; set; } = "7.0";

    public string NodeType { get; set; } = "RED1-MICRO";

    public int ClusterSize { get; set; } = 1;

    public bool ClusterEnabled { get; set; }

    public string[]? Tags { get; set; }

    public ScalewayZone Zone { get; set; } = ScalewayZone.FrPar1;

    public ParameterResource PasswordParameter { get; } = passwordParameter;

    internal EndpointReference PrimaryEndpoint => new(this, "tcp");

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)},password={PasswordParameter}"
        );
}
