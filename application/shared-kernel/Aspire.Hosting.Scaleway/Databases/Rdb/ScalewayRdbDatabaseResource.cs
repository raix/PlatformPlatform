namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a database within a Scaleway RDB instance.
/// </summary>
public sealed class ScalewayRdbDatabaseResource(string name, string databaseName, ScalewayRdbInstanceResource parent)
    : Resource(name), IResourceWithParent<ScalewayRdbInstanceResource>, IResourceWithConnectionString
{
    public ScalewayRdbInstanceResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    public string DatabaseName { get; } = databaseName;

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Parent};Database={DatabaseName}");
}
