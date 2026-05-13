namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Annotation that marks a resource for provisioning as a Scaleway Managed Database (RDB) instance.
/// </summary>
public sealed class PublishAsScalewayRdbAnnotation : IScalewayPublishTargetAnnotation
{
    public ScalewayRdbPublishConfig Config { get; init; } = new();
}

/// <summary>
///     Configuration for publishing a Scaleway RDB instance.
///     Platform teams can set these values per environment via appsettings or callbacks.
/// </summary>
public sealed class ScalewayRdbPublishConfig
{
    public string Engine { get; set; } = "PostgreSQL-16";

    public string NodeType { get; set; } = "DB-DEV-S";

    public string UserName { get; set; } = "admin";

    public bool IsHaCluster { get; set; }

    public bool DisableBackup { get; set; }

    public long VolumeSizeInGb { get; set; } = 5;

    public string[]? Tags { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public DeletionPolicy DeletionPolicy { get; set; } = DeletionPolicy.Retain;
}
