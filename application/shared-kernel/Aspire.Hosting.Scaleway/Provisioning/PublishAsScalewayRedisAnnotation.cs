namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Annotation that marks a resource for provisioning as a Scaleway Managed Redis cluster.
/// </summary>
public sealed class PublishAsScalewayRedisAnnotation : IScalewayPublishTargetAnnotation
{
    public ScalewayRedisPublishConfig Config { get; init; } = new();
}

/// <summary>
///     Configuration for publishing a Scaleway Redis cluster.
/// </summary>
public sealed class ScalewayRedisPublishConfig
{
    public string Version { get; init; } = "7.0";

    public string NodeType { get; set; } = "RED1-MICRO";

    public int ClusterSize { get; set; } = 1;

    public bool TlsEnabled { get; set; } = true;

    public string[]? Tags { get; set; }

    public ScalewayZone Zone { get; set; } = ScalewayZone.FrPar1;

    public DeletionPolicy DeletionPolicy { get; set; } = DeletionPolicy.Retain;
}
