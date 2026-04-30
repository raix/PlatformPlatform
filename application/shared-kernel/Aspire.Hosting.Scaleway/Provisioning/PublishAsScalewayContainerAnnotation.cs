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
    public long MemoryLimitMb { get; init; } = 256;

    public long CpuLimitMillicores { get; init; } = 140;

    public int MinScale { get; init; }

    public int MaxScale { get; init; } = 20;

    public int MaxConcurrency { get; set; } = 50;

    public int TimeoutSeconds { get; set; } = 300;

    public int Port { get; init; } = 8080;

    public string Privacy { get; set; } = "public";

    public string? HealthCheckPath { get; set; }

    public string? RegistryNamespace { get; set; }

    public string[]? Tags { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public DeletionPolicy DeletionPolicy { get; set; } = DeletionPolicy.Delete;
}
