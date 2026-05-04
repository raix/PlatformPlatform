namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Decides what to do with each per-resource change before <see cref="ScalewayDeploymentStep" />
///     applies it. Used to opt into a step-by-step deploy mode for manual QA.
/// </summary>
public interface IDeployApprover
{
    Task<DeployApproverDecision> ApproveAsync(string resourceName, string resourceType, CancellationToken cancellationToken);
}

public enum DeployApproverDecision
{
    /// <summary>Apply this change and continue.</summary>
    Apply,

    /// <summary>Skip this change but continue with the rest of the plan.</summary>
    Skip,

    /// <summary>Abort the entire deploy.</summary>
    Abort
}
