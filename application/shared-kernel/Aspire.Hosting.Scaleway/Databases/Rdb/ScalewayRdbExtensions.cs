using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayRdbExtensions
{
    /// <summary>
    /// Adds a Scaleway Managed Database (RDB) instance to the application model.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> AddScalewayRdbInstance(
        this IDistributedApplicationBuilder builder,
        string name,
        string engine = "PostgreSQL-16",
        string nodeType = "DB-DEV-S",
        IResourceBuilder<ParameterResource>? password = null)
    {
        var passwordParameter = password?.Resource
            ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        var resource = new ScalewayRdbInstanceResource(name, passwordParameter)
        {
            Engine = engine,
            NodeType = nodeType
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Adds a database to a Scaleway RDB instance.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbDatabaseResource> AddDatabase(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        string name,
        string? databaseName = null)
    {
        var actualDatabaseName = databaseName ?? name;
        var parent = builder.Resource;

        parent.AddDatabase(name, actualDatabaseName);

        var resource = new ScalewayRdbDatabaseResource(name, actualDatabaseName, parent);
        return builder.ApplicationBuilder.AddResource(resource);
    }

    /// <summary>
    /// Sets the Scaleway region for the RDB instance.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> WithRegion(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        ScalewayRegion region)
    {
        builder.Resource.Region = region;
        return builder;
    }

    /// <summary>
    /// Enables high availability for the RDB instance.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> WithHighAvailability(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder)
    {
        builder.Resource.IsHighAvailabilityCluster = true;
        return builder;
    }

    /// <summary>
    /// Sets the node type (compute tier) for the RDB instance.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> WithNodeType(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        string nodeType)
    {
        builder.Resource.NodeType = nodeType;
        return builder;
    }

    /// <summary>
    /// Sets the volume size for the RDB instance.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> WithVolumeSize(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        long sizeInGb)
    {
        builder.Resource.VolumeSizeInGb = sizeInGb;
        return builder;
    }

    /// <summary>
    /// Disables automatic backups for the RDB instance.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> WithBackupDisabled(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder)
    {
        builder.Resource.DisableBackup = true;
        return builder;
    }
}
