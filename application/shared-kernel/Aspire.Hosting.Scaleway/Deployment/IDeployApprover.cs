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

/// <summary>
///     Default approver: applies every change without prompting. Matches the historical
///     non-interactive behaviour of <see cref="ScalewayDeploymentStep" />.
/// </summary>
internal sealed class AutoApprover : IDeployApprover
{
    public Task<DeployApproverDecision> ApproveAsync(string resourceName, string resourceType, CancellationToken cancellationToken)
    {
        return Task.FromResult(DeployApproverDecision.Apply);
    }
}

/// <summary>
///     Console-prompting approver used during manual QA. Prints each change and reads
///     a single character from stdin to decide. Falls back to <see cref="DeployApproverDecision.Abort" />
///     on EOF or on inputs that are neither apply nor skip.
/// </summary>
internal sealed class InteractiveConsoleApprover : IDeployApprover
{
    public Task<DeployApproverDecision> ApproveAsync(string resourceName, string resourceType, CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine($"Next change: {resourceType} '{resourceName}'");
        Console.Write("[a]pply / [s]kip / [q]uit > ");

        var line = Console.ReadLine();
        var decision = line?.Trim().ToLowerInvariant() switch
        {
            "" or "a" or "apply" => DeployApproverDecision.Apply,
            "s" or "skip" => DeployApproverDecision.Skip,
            _ => DeployApproverDecision.Abort
        };
        return Task.FromResult(decision);
    }
}
