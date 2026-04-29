namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Complete deployment plan combining infrastructure changes, cost estimates, and budget validation.
/// Produced by the deployment planner for dry-run review before applying changes.
/// </summary>
public sealed record DeploymentPlan(
    IReadOnlyList<DeploymentChange> Changes,
    DeploymentCostSummary? CostSummary,
    BudgetCheckResult? BudgetCheck)
{
    public bool HasBlockedChanges => Changes.Any(c => c.IsBlocked);

    public bool ExceedsBudget => BudgetCheck?.ExceedsBudget == true;

    public bool CanDeploy => !HasBlockedChanges && !ExceedsBudget;
}

public sealed record BudgetCheckResult(decimal Budget, decimal EstimatedCost, string Currency)
{
    public bool ExceedsBudget => EstimatedCost > Budget;

    public string Message => ExceedsBudget
        ? $"Estimated monthly cost €{EstimatedCost:F2} exceeds budget of €{Budget:F2}."
        : $"Estimated monthly cost €{EstimatedCost:F2} is within budget of €{Budget:F2}.";
}
