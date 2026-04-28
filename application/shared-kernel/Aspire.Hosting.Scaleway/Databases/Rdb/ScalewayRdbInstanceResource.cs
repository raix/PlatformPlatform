namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a Scaleway Managed Database (RDB) instance.
/// Supports PostgreSQL and MySQL engines.
/// </summary>
public sealed class ScalewayRdbInstanceResource(string name, ParameterResource passwordParameter)
    : Resource(name), IScalewayResource, IResourceWithConnectionString, IResourceWithEndpoints
{
    private readonly Dictionary<string, string> _databases = new();

    public ScalewayCredentialConfig? CredentialConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public string Engine { get; set; } = "PostgreSQL-16";

    public string NodeType { get; set; } = "DB-DEV-S";

    public string UserName { get; set; } = "admin";

    public ParameterResource PasswordParameter { get; } = passwordParameter;

    public bool IsHighAvailabilityCluster { get; set; }

    public bool DisableBackup { get; set; }

    public long VolumeSizeInGb { get; set; } = 5;

    public string[]? Tags { get; set; }

    public ScalewayRegion Region { get; set; } = ScalewayRegion.FrPar;

    public IReadOnlyDictionary<string, string> Databases => _databases;

    internal EndpointReference PrimaryEndpoint => new(this, "tcp");

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"Host={PrimaryEndpoint.Property(EndpointProperty.Host)};Port={PrimaryEndpoint.Property(EndpointProperty.Port)};Username={UserName};Password={PasswordParameter}"
        );

    internal void AddDatabase(string resourceName, string databaseName)
    {
        _databases.TryAdd(resourceName, databaseName);
    }
}
