namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Annotation that marks a project resource for deployment as a Scaleway Serverless Container.
/// </summary>
public sealed class PublishAsScalewayContainerAnnotation : IScalewayPublishTargetAnnotation
{
    public ScalewayContainerPublishConfig Config { get; init; } = new();
}

/// <summary>
///     Configuration for publishing a Scaleway Serverless Container.
/// </summary>
public sealed class ScalewayContainerPublishConfig
{
    public long MemoryLimitMb { get; set; } = 256;

    public long CpuLimitMillicores { get; init; } = 140;

    public int MinScale { get; set; }

    public int MaxScale { get; set; } = 20;

    public int MaxConcurrency { get; set; } = 50;

    public int TimeoutSeconds { get; set; } = 300;

    public int Port { get; init; } = 8080;

    public string Privacy { get; set; } = "public";

    /// <summary>
    ///     Path that Scaleway hits to decide whether the container is healthy. Scaleway Serverless
    ///     Containers expose a single check (no separate liveness/readiness), so this points at the
    ///     simpler self-check (<c>/internal-api/live</c>) — the dependency-aware <c>/internal-api/ready</c>
    ///     check would let a transient downstream blip take all containers down.
    /// </summary>
    public string HealthCheckPath { get; set; } = "/internal-api/live";

    /// <summary>Seconds between Scaleway health-check probes.</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>Consecutive failures before Scaleway marks the container unhealthy.</summary>
    public int HealthCheckFailureThreshold { get; set; } = 3;

    public string? RegistryNamespace { get; set; }

    public string[]? Tags { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public DeletionPolicy DeletionPolicy { get; set; } = DeletionPolicy.Delete;
}
