namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Represents a single change detected during deployment planning.
/// Used by the dry-run and deployment steps to report what would happen.
/// </summary>
public sealed record DeploymentChange(
    string ResourceName,
    DeploymentChangeType ChangeType,
    DeploymentChangeSeverity Severity,
    string Description)
{
    public bool IsBlocked => Severity == DeploymentChangeSeverity.Blocked;
}

public enum DeploymentChangeType
{
    Create,
    Update,
    Delete,
    NoChange
}

public enum DeploymentChangeSeverity
{
    /// <summary>
    /// Safe to apply automatically (e.g., scaling, tags).
    /// </summary>
    Safe,

    /// <summary>
    /// Requires attention but can proceed (e.g., node type change may cause brief downtime).
    /// </summary>
    Warning,

    /// <summary>
    /// Blocked - would cause data loss or requires manual intervention (e.g., region change, deletion of database).
    /// </summary>
    Blocked
}
