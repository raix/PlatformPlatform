namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     String identifiers for Scaleway resource categories. Used as keys into
///     <see cref="DeploymentPlanner.GetDefaultDeletionPolicy" /> immutable/warning field maps and
///     as the value passed to <see cref="IDeployApprover.ApproveAsync" />. A typo at any callsite
///     would silently allow a destructive change through, so all references resolve through here.
/// </summary>
public static class ScalewayResourceTypes
{
    public const string Rdb = "rdb";
    public const string Redis = "redis";
    public const string Container = "container";
    public const string ContainerNamespace = "container-namespace";
    public const string ObjectStorage = "object-storage";
    public const string PrivateNetwork = "private-network";
    public const string Registry = "registry";
    public const string Secret = "secret";
}
