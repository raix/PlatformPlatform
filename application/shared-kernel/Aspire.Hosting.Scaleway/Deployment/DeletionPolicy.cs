namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Controls what happens to a cloud resource when it's removed from the AppHost.
/// </summary>
public enum DeletionPolicy
{
    /// <summary>
    ///     The resource is kept in Scaleway even if removed from the AppHost.
    ///     This is the default for data-bearing resources (databases, storage).
    /// </summary>
    Retain,

    /// <summary>
    ///     The resource is deleted from Scaleway when removed from the AppHost.
    ///     Only safe for stateless resources (containers, DNS records).
    /// </summary>
    Delete
}
