namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Default approver: applies every change without prompting. Used for non-interactive
///     deploys (CI, production rollouts).
/// </summary>
internal sealed class AutoApprover : IDeployApprover
{
    public Task<DeployApproverDecision> ApproveAsync(string resourceName, string resourceType, CancellationToken cancellationToken)
    {
        return Task.FromResult(DeployApproverDecision.Apply);
    }
}
