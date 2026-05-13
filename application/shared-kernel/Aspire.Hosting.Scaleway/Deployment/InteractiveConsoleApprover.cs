namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Console-prompting approver used during manual QA. Prints each change and reads
///     a single character from stdin to decide. Falls back to <see cref="DeployApproverDecision.Abort" />
///     on EOF or on inputs that are neither apply nor skip.
/// </summary>
internal sealed class InteractiveConsoleApprover : IDeployApprover
{
    public Task<DeployApproverDecision> ApproveAsync(string resourceName, string resourceType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Console.WriteLine();
        Console.WriteLine($"Next change: {resourceType} '{resourceName}'");
        Console.Write("[a]pply / [s]kip / [q]uit > ");

        // Console.ReadLine() blocks; mid-prompt cancellation falls through to process termination.
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
