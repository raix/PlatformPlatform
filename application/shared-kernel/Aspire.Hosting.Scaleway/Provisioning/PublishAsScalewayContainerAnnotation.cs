namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Annotation that marks a project resource for deployment as a Scaleway Serverless Container.
/// </summary>
public sealed class PublishAsScalewayContainerAnnotation : IScalewayPublishTargetAnnotation
{
    public ScalewayContainerPublishConfig Config { get; set; } = new();
}

/// <summary>
/// Configuration for publishing a Scaleway Serverless Container.
/// </summary>
public sealed class ScalewayContainerPublishConfig
{
    public long MemoryLimitMb { get; set; } = 256;

    public long CpuLimitMillicores { get; set; } = 140;

    public int MinScale { get; set; }

    public int MaxScale { get; set; } = 20;

    public int MaxConcurrency { get; set; } = 50;

    public int TimeoutSeconds { get; set; } = 300;

    public int Port { get; set; } = 8080;

    public string Privacy { get; set; } = "public";

    public string? HealthCheckPath { get; set; }

    public string? RegistryNamespace { get; set; }

    public string[]? Tags { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public DeletionPolicy DeletionPolicy { get; set; } = DeletionPolicy.Delete;
}
