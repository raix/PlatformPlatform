using System.Globalization;
using System.Text;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Pipeline step that drives the Scaleway deployment for a single
///     <see cref="ScalewayEnvironmentResource" /> when running <c>aspire deploy</c>.
/// </summary>
internal static class ScalewayPipelineStep
{
    public const string StepNamePrefix = "scaleway-deploy";

    public static string StepNameFor(ScalewayEnvironmentResource environment)
    {
        return $"{StepNamePrefix}-{environment.Name}";
    }

    public static Task ExecuteAsync(ScalewayEnvironmentResource environment, PipelineStepContext context)
    {
        return RunAsync(
            environment,
            context.Model.Resources,
            context.Logger,
            (key, markdown) => context.Summary.Add(key, new MarkdownString(markdown)),
            null,
            context.CancellationToken
        );
    }

    internal static async Task RunAsync(
        ScalewayEnvironmentResource environment,
        IEnumerable<IResource> allResources,
        ILogger logger,
        Action<string, string> summaryWriter,
        Func<IReadOnlyList<IResource>, CancellationToken, Task<DeploymentCostSummary>>? costEstimator,
        CancellationToken cancellationToken)
    {
        var publishResources = allResources
            .Where(r => r.Annotations.OfType<IScalewayPublishTargetAnnotation>().Any())
            .ToArray();

        if (publishResources.Length == 0)
        {
            logger.LogInformation("No Scaleway publish targets found in environment '{Environment}'; nothing to deploy.", environment.Name);
            return;
        }

        var config = environment.CredentialConfig;
        var missingCredentials = new List<string>();
        if (string.IsNullOrEmpty(config.AccessKey)) missingCredentials.Add("SCW_ACCESS_KEY");
        if (string.IsNullOrEmpty(config.SecretKey)) missingCredentials.Add("SCW_SECRET_KEY");
        if (string.IsNullOrEmpty(config.DefaultProjectId)) missingCredentials.Add("SCW_DEFAULT_PROJECT_ID");
        if (missingCredentials.Count > 0)
        {
            throw new DistributedApplicationException(
                $"Scaleway credentials missing for environment '{environment.Name}'. Set the following environment variables before running aspire deploy: {string.Join(", ", missingCredentials)}."
            );
        }

        using var apiClient = new ScalewayApiClient(config);

        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, publishResources, apiClient, cancellationToken);

        DeploymentCostSummary costSummary;
        if (costEstimator is not null)
        {
            costSummary = await costEstimator(publishResources, cancellationToken);
        }
        else
        {
            using var pricing = new ScalewayPricingClient();
            costSummary = await pricing.EstimateDeploymentCostAsync(publishResources, config.DefaultRegion, cancellationToken);
        }

        BudgetCheckResult? budgetCheck = null;
        if (ResolveMonthlyBudget(environment) is { } budget)
        {
            budgetCheck = new BudgetCheckResult(budget, costSummary.TotalMonthlyPrice, "EUR");
        }

        var plan = new DeploymentPlan(changes, costSummary, budgetCheck);

        var planMarkdown = FormatPlan(environment, plan);
        logger.LogInformation("Scaleway deployment plan for '{Environment}':\n{Plan}", environment.Name, planMarkdown);
        summaryWriter($"Scaleway: {environment.Name}", planMarkdown);

        if (!plan.CanDeploy)
        {
            var reasons = new List<string>();
            if (plan.HasBlockedChanges)
            {
                var blocked = plan.Changes.Where(c => c.IsBlocked).Select(c => $"'{c.ResourceName}': {c.Description}");
                reasons.Add($"blocked changes: {string.Join("; ", blocked)}");
            }

            if (plan.BudgetCheck is { ExceedsBudget: true } overBudget)
            {
                reasons.Add(overBudget.Message);
            }

            throw new DistributedApplicationException(
                $"Scaleway deploy aborted for environment '{environment.Name}': {string.Join(" | ", reasons)}"
            );
        }

        // Dry-run mode: produce the plan, then exit. Exit code reflects whether actual drift
        // was found — used by the weekly drift-detection cron to fail-red on diff and post an
        // alert. NoChange entries don't count as drift.
        if (Environment.GetEnvironmentVariable("SCW_DEPLOY_DRY_RUN") == "1")
        {
            var driftChanges = plan.Changes.Where(c => c.ChangeType != DeploymentChangeType.NoChange).ToArray();
            if (driftChanges.Length > 0)
            {
                throw new DistributedApplicationException(
                    $"Drift detected in environment '{environment.Name}': {driftChanges.Length} change(s) pending. See plan above."
                );
            }

            logger.LogInformation("Dry-run for '{Environment}' complete — no drift.", environment.Name);
            return;
        }

        await ScalewayDeploymentStep.DeployAsync(environment, publishResources, apiClient, SelectApprover(), cancellationToken);
    }

    /// <summary>
    ///     Returns the budget that should gate this deploy. <c>SCW_MONTHLY_BUDGET</c> always
    ///     wins over <see cref="ScalewayEnvironmentResource.MonthlyBudget" /> so QA runs can
    ///     dial the gate up or down without editing AppHost code.
    /// </summary>
    internal static decimal? ResolveMonthlyBudget(ScalewayEnvironmentResource environment)
    {
        var fromEnv = Environment.GetEnvironmentVariable("SCW_MONTHLY_BUDGET");
        if (fromEnv is not null && decimal.TryParse(fromEnv, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return environment.MonthlyBudget;
    }

    /// <summary>
    ///     Selects an interactive approver only when explicitly opted in and a real terminal is
    ///     attached on both stdin and stdout. CI subprocesses (any redirection) silently fall back
    ///     to <see cref="AutoApprover" />.
    /// </summary>
    internal static IDeployApprover SelectApprover()
    {
        var optIn = Environment.GetEnvironmentVariable("SCW_DEPLOY_INTERACTIVE") == "1";
        if (optIn && !Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            return new InteractiveConsoleApprover();
        }

        return new AutoApprover();
    }

    internal static string FormatPlan(ScalewayEnvironmentResource environment, DeploymentPlan plan)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"### Changes ({plan.Changes.Count})");
        if (plan.Changes.Count == 0)
        {
            sb.AppendLine("- _none_");
        }
        else
        {
            foreach (var change in plan.Changes)
            {
                var marker = change.Severity switch
                {
                    DeploymentChangeSeverity.Blocked => "[BLOCKED]",
                    DeploymentChangeSeverity.Warning => "[WARN]",
                    _ => "[OK]"
                };
                sb.AppendLine($"- {marker} **{change.ChangeType}** `{change.ResourceName}` — {change.Description}");
            }
        }

        if (plan.CostSummary is { } costs)
        {
            sb.AppendLine();
            sb.AppendLine($"### Estimated monthly cost: €{costs.TotalMonthlyPrice:F2}");
            foreach (var estimate in costs.Estimates)
            {
                sb.AppendLine($"- `{estimate.ResourceName}` — €{estimate.MonthlyPrice:F2} ({estimate.Details})");
            }
        }

        if (plan.BudgetCheck is { } budget)
        {
            sb.AppendLine();
            sb.AppendLine(budget.ExceedsBudget ? $"### [OVER BUDGET] {budget.Message}" : $"### [WITHIN BUDGET] {budget.Message}");
        }
        else if (plan.CostSummary is not null)
        {
            sb.AppendLine();
            sb.AppendLine("> Tip: set a monthly cap with `WithMonthlyBudget(<eur>)` in AppHost or `SCW_MONTHLY_BUDGET=<eur>` to abort deploys above the cap.");
        }

        return sb.ToString().TrimEnd();
    }
}
