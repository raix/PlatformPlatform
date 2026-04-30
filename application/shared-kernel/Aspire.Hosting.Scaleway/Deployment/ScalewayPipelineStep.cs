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

        EnsureCredentials(environment);

        using var apiClient = new ScalewayApiClient(environment.CredentialConfig);

        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, publishResources, apiClient, cancellationToken);

        DeploymentCostSummary? costSummary = null;
        BudgetCheckResult? budgetCheck = null;
        var effectiveBudget = ResolveMonthlyBudget(environment);
        if (effectiveBudget is { } budget)
        {
            costSummary = costEstimator is not null
                ? await costEstimator(publishResources, cancellationToken)
                : await EstimateWithDefaultClientAsync(publishResources, environment, cancellationToken);
            budgetCheck = new BudgetCheckResult(budget, costSummary.TotalMonthlyPrice, "EUR");
        }

        var plan = new DeploymentPlan(changes, costSummary, budgetCheck);

        var planMarkdown = FormatPlan(environment, plan);
        logger.LogInformation("Scaleway deployment plan for '{Environment}':\n{Plan}", environment.Name, planMarkdown);
        summaryWriter($"Scaleway: {environment.Name}", planMarkdown);

        if (!plan.CanDeploy)
        {
            throw new DistributedApplicationException(BuildBlockedMessage(environment, plan));
        }

        var approver = SelectApprover();
        await ScalewayDeploymentStep.DeployAsync(environment, publishResources, apiClient, approver, cancellationToken);
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
    ///     attached. CI subprocesses (no TTY) silently fall back to <see cref="AutoApprover" />.
    /// </summary>
    internal static IDeployApprover SelectApprover()
    {
        var optIn = Environment.GetEnvironmentVariable("SCALEWAY_DEPLOY_INTERACTIVE") == "1";
        if (optIn && !Console.IsInputRedirected)
        {
            return new InteractiveConsoleApprover();
        }

        return new AutoApprover();
    }

    private static async Task<DeploymentCostSummary> EstimateWithDefaultClientAsync(
        IReadOnlyList<IResource> resources,
        ScalewayEnvironmentResource environment,
        CancellationToken cancellationToken)
    {
        using var pricing = new ScalewayPricingClient();
        return await pricing.EstimateDeploymentCostAsync(resources, environment.CredentialConfig.DefaultRegion, cancellationToken);
    }

    internal static void EnsureCredentials(ScalewayEnvironmentResource environment)
    {
        var config = environment.CredentialConfig;
        var missing = new List<string>();
        if (string.IsNullOrEmpty(config.AccessKey)) missing.Add("SCW_ACCESS_KEY");
        if (string.IsNullOrEmpty(config.SecretKey)) missing.Add("SCW_SECRET_KEY");
        if (string.IsNullOrEmpty(config.DefaultProjectId)) missing.Add("SCW_DEFAULT_PROJECT_ID");

        if (missing.Count > 0)
        {
            throw new DistributedApplicationException(
                $"Scaleway credentials missing for environment '{environment.Name}'. Set the following environment variables before running aspire deploy: {string.Join(", ", missing)}."
            );
        }
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
                    DeploymentChangeSeverity.Blocked => "🚫",
                    DeploymentChangeSeverity.Warning => "⚠️",
                    _ => "•"
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
                sb.AppendLine($"- `{estimate.ResourceType}` — €{estimate.MonthlyPrice:F2} ({estimate.Details})");
            }
        }

        if (plan.BudgetCheck is { } budget)
        {
            sb.AppendLine();
            sb.AppendLine(budget.ExceedsBudget ? $"### 🚫 {budget.Message}" : $"### ✅ {budget.Message}");
        }

        return sb.ToString().TrimEnd();
    }

    internal static string BuildBlockedMessage(ScalewayEnvironmentResource environment, DeploymentPlan plan)
    {
        var reasons = new List<string>();

        if (plan.HasBlockedChanges)
        {
            var blocked = plan.Changes.Where(c => c.IsBlocked).Select(c => $"'{c.ResourceName}': {c.Description}");
            reasons.Add($"blocked changes: {string.Join("; ", blocked)}");
        }

        if (plan.BudgetCheck is { ExceedsBudget: true } budget)
        {
            reasons.Add(budget.Message);
        }

        return $"Scaleway deploy aborted for environment '{environment.Name}': {string.Join(" | ", reasons)}";
    }
}
