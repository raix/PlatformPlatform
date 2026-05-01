using Aspire.Hosting.Pipelines;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayPipelineStepTests : IDisposable
{
    private readonly ScalewayMockServer _mockServer = new();

    public ScalewayPipelineStepTests()
    {
        _mockServer.Start();
    }

    public void Dispose()
    {
        _mockServer.Dispose();
    }

    [Fact]
    public void AddScalewayEnvironment_RegistersPipelineStepRequiredByDeploy()
    {
        var builder = DistributedApplication.CreateBuilder();

        var environment = builder.AddScalewayEnvironment("production");

        var annotation = environment.Resource.Annotations.OfType<PipelineStepAnnotation>().FirstOrDefault();
        annotation.Should().NotBeNull("AddScalewayEnvironment should attach a pipeline step factory to the environment resource");
    }

    [Fact]
    public async Task RunAsync_WithMissingCredentials_ThrowsDistributedApplicationException()
    {
        var environment = new ScalewayEnvironmentResource(
            "production",
            new ScalewayCredentialConfig { ApiUrl = _mockServer.Url, DefaultRegion = ScalewayRegion.FrPar },
            true
        );
        var rdb = CreateRdbResource("my-db", new ScalewayRdbPublishConfig());

        var act = async () => await ScalewayPipelineStep.RunAsync(
            environment, [rdb], NullLogger.Instance, NoOpSummary, ZeroCostEstimator, CancellationToken.None
        );

        await act.Should().ThrowAsync<DistributedApplicationException>()
            .Where(ex => ex.Message.Contains("SCW_ACCESS_KEY") && ex.Message.Contains("SCW_SECRET_KEY") && ex.Message.Contains("SCW_DEFAULT_PROJECT_ID"));
    }

    [Fact]
    public async Task RunAsync_WithNoPublishTargets_LogsAndReturnsWithoutChecks()
    {
        var environment = new ScalewayEnvironmentResource(
            "production",
            new ScalewayCredentialConfig { ApiUrl = _mockServer.Url, DefaultRegion = ScalewayRegion.FrPar },
            true
        );
        var unrelated = new ScalewayRdbInstanceResource("local-only");

        await ScalewayPipelineStep.RunAsync(
            environment, [unrelated], NullLogger.Instance, NoOpSummary, ZeroCostEstimator, CancellationToken.None
        );

        _mockServer.ReceivedRequests.Should().BeEmpty("no API calls should be made when there are no publish targets");
    }

    [Fact]
    public async Task RunAsync_WithCleanPlan_CallsDeployAsync()
    {
        var environment = CreateEnvironment("production");
        var rdb = CreateRdbResource("my-db", new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" });

        await ScalewayPipelineStep.RunAsync(
            environment, [rdb], NullLogger.Instance, NoOpSummary, ZeroCostEstimator, CancellationToken.None
        );

        _mockServer.ReceivedRequests.Where(r => r.Method == "POST").Should().NotBeEmpty("a clean plan should result in POST calls to provision resources");
        _mockServer.Resources.Should().ContainKey("instances");
    }

    [Fact]
    public async Task RunAsync_WithBlockedChange_ThrowsBeforeDeploy()
    {
        var environment = CreateEnvironment("production");

        // Pre-populate the mock server with an existing RDB whose engine differs (immutable field → blocked)
        await ScalewayPipelineStep.RunAsync(
            environment,
            [CreateRdbResource("my-db", new ScalewayRdbPublishConfig { Engine = "PostgreSQL-15", NodeType = "DB-DEV-S" })],
            NullLogger.Instance, NoOpSummary, ZeroCostEstimator, CancellationToken.None
        );
        var requestsBeforeBlockedAttempt = _mockServer.ReceivedRequests.Count(r => r.Method == "POST");

        // Attempt to deploy the same resource with a different (immutable) engine → blocked update
        var conflictingRdb = CreateRdbResource("my-db", new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" });

        var act = async () => await ScalewayPipelineStep.RunAsync(
            environment, [conflictingRdb], NullLogger.Instance, NoOpSummary, ZeroCostEstimator, CancellationToken.None
        );

        await act.Should().ThrowAsync<DistributedApplicationException>()
            .Where(ex => ex.Message.Contains("blocked changes") && ex.Message.Contains("'my-db'"));

        var requestsAfterBlockedAttempt = _mockServer.ReceivedRequests.Count(r => r.Method == "POST");
        requestsAfterBlockedAttempt.Should().Be(requestsBeforeBlockedAttempt, "no new POSTs should be made when the plan is blocked");
    }

    [Fact]
    public async Task RunAsync_WhenBudgetExceeded_ThrowsBeforeDeploy()
    {
        var environment = CreateEnvironment("production");
        environment.MonthlyBudget = 10m;

        var rdb = CreateRdbResource("my-db", new ScalewayRdbPublishConfig());

        var fakeCostSummary = new DeploymentCostSummary(
            [new CostEstimate("my-db", "DB-DEV-S", 99m, "EUR", "DB-DEV-S")],
            99m,
            "EUR"
        );

        var act = async () => await ScalewayPipelineStep.RunAsync(
            environment, [rdb], NullLogger.Instance, NoOpSummary,
            (_, _) => Task.FromResult(fakeCostSummary),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<DistributedApplicationException>()
            .Where(ex => ex.Message.Contains("exceeds budget"));

        _mockServer.ReceivedRequests.Where(r => r.Method == "POST").Should().BeEmpty("no POSTs should be made when budget is exceeded");
    }

    [Fact]
    public void FormatPlan_WithCostsButNoBudget_IncludesDiscoveryHint()
    {
        var environment = CreateEnvironment("production");
        var changes = new[]
        {
            new DeploymentChange("my-db", DeploymentChangeType.Create, DeploymentChangeSeverity.Safe, "Create rdb 'my-db'")
        };
        var costs = new DeploymentCostSummary(
            [new CostEstimate("my-db", "DB-DEV-S", 12m, "EUR", "DB-DEV-S")],
            12m, "EUR"
        );
        var plan = new DeploymentPlan(changes, costs, null);

        var output = ScalewayPipelineStep.FormatPlan(environment, plan);

        output.Should().Contain("€12.00", "cost should always render even without a budget");
        output.Should().Contain("WithMonthlyBudget", "the hint should mention the AppHost API");
        output.Should().Contain("SCW_MONTHLY_BUDGET", "the hint should mention the env var");
        output.Should().NotContain("within budget", "no budget verdict should appear without a budget");
    }

    [Fact]
    public void FormatPlan_RendersChangesCostsAndBudget()
    {
        var environment = CreateEnvironment("production");
        var changes = new[]
        {
            new DeploymentChange("my-db", DeploymentChangeType.Create, DeploymentChangeSeverity.Safe, "Create rdb 'my-db'")
        };
        var costs = new DeploymentCostSummary(
            [new CostEstimate("my-db", "DB-DEV-S", 12m, "EUR", "DB-DEV-S")],
            12m, "EUR"
        );
        var plan = new DeploymentPlan(changes, costs, new BudgetCheckResult(50m, 12m, "EUR"));

        var output = ScalewayPipelineStep.FormatPlan(environment, plan);

        output.Should().Contain("Create").And.Contain("my-db");
        output.Should().Contain("€12.00");
        output.Should().Contain("within budget");
    }

    [Fact]
    public void StepNameFor_IsScopedPerEnvironment()
    {
        var production = new ScalewayEnvironmentResource("production", new ScalewayCredentialConfig(), true);
        var staging = new ScalewayEnvironmentResource("staging", new ScalewayCredentialConfig(), true);

        ScalewayPipelineStep.StepNameFor(production).Should().NotBe(ScalewayPipelineStep.StepNameFor(staging));
        ScalewayPipelineStep.StepNameFor(production).Should().Contain("production");
    }

    [Fact]
    public void ResolveMonthlyBudget_WithEnvVar_OverridesConfiguredBudget()
    {
        var environment = new ScalewayEnvironmentResource("production", new ScalewayCredentialConfig(), true)
        {
            MonthlyBudget = 100m
        };

        Environment.SetEnvironmentVariable("SCW_MONTHLY_BUDGET", "25.50");
        try
        {
            ScalewayPipelineStep.ResolveMonthlyBudget(environment).Should().Be(25.50m);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCW_MONTHLY_BUDGET", null);
        }
    }

    [Fact]
    public void ResolveMonthlyBudget_WithoutEnvVar_ReturnsConfiguredBudget()
    {
        var environment = new ScalewayEnvironmentResource("production", new ScalewayCredentialConfig(), true)
        {
            MonthlyBudget = 100m
        };

        Environment.SetEnvironmentVariable("SCW_MONTHLY_BUDGET", null);
        ScalewayPipelineStep.ResolveMonthlyBudget(environment).Should().Be(100m);
    }

    [Fact]
    public void SelectApprover_WhenInteractiveOptInAndStdinIsRedirected_FallsBackToAutoApprover()
    {
        Environment.SetEnvironmentVariable("SCW_DEPLOY_INTERACTIVE", "1");
        try
        {
            // The xUnit runner redirects stdin, so the TTY check fails and we get AutoApprover.
            ScalewayPipelineStep.SelectApprover().Should().BeOfType<AutoApprover>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCW_DEPLOY_INTERACTIVE", null);
        }
    }

    [Fact]
    public void SelectApprover_WithoutOptIn_ReturnsAutoApprover()
    {
        Environment.SetEnvironmentVariable("SCW_DEPLOY_INTERACTIVE", null);
        ScalewayPipelineStep.SelectApprover().Should().BeOfType<AutoApprover>();
    }

    [Fact]
    public async Task DeployAsync_WhenApproverSkipsRdb_DoesNotCreateInstance()
    {
        var environment = CreateEnvironment("production");
        var rdb = CreateRdbResource("my-db", new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" });

        var approver = new ScriptedApprover([DeployApproverDecision.Skip]);
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], approver);

        _mockServer.Resources.Should().NotContainKey("instances", "the rdb POST should be skipped when approver returns Skip");
    }

    [Fact]
    public async Task DeployAsync_WhenApproverAborts_ThrowsBeforeApplyingRemainingResources()
    {
        var environment = CreateEnvironment("production");
        var rdb = CreateRdbResource("first-db", new ScalewayRdbPublishConfig());
        var rdb2 = CreateRdbResource("second-db", new ScalewayRdbPublishConfig());

        var approver = new ScriptedApprover([DeployApproverDecision.Apply, DeployApproverDecision.Abort]);

        var act = async () => await ScalewayDeploymentStep.DeployAsync(environment, [rdb, rdb2], approver);

        await act.Should().ThrowAsync<DistributedApplicationException>()
            .Where(ex => ex.Message.Contains("aborted") && ex.Message.Contains("'second-db'"));

        // The first resource was applied; the second was where we aborted, so its POST never happened.
        _mockServer.Resources.GetValueOrDefault("instances", []).Should().HaveCount(1);
    }

    private ScalewayEnvironmentResource CreateEnvironment(string name)
    {
        var config = new ScalewayCredentialConfig
        {
            AccessKey = "SCW-PIPELINE-ACCESS-KEY",
            SecretKey = "pipeline-secret-key",
            DefaultProjectId = "pipeline-project",
            DefaultRegion = ScalewayRegion.FrPar,
            ApiUrl = _mockServer.Url
        };
        return new ScalewayEnvironmentResource(name, config, true);
    }

    private static ScalewayRdbInstanceResource CreateRdbResource(string name, ScalewayRdbPublishConfig config)
    {
        var rdb = new ScalewayRdbInstanceResource(name);
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation { Config = config });
        return rdb;
    }

    private static void NoOpSummary(string key, string value)
    {
    }

    /// <summary>
    ///     Cost estimator stand-in that returns zero — keeps unit tests deterministic and
    ///     off the network. Tests that exercise budget/cost behaviour inject a specific summary.
    /// </summary>
    private static Task<DeploymentCostSummary> ZeroCostEstimator(IReadOnlyList<IResource> _, CancellationToken __)
    {
        return Task.FromResult(new DeploymentCostSummary([], 0m, "EUR"));
    }

    /// <summary>
    ///     Test approver that returns a pre-scripted sequence of decisions, one per call.
    ///     Exhausting the script throws — tests should script as many decisions as resources.
    /// </summary>
    private sealed class ScriptedApprover(IReadOnlyList<DeployApproverDecision> decisions) : IDeployApprover
    {
        private int _index;

        public Task<DeployApproverDecision> ApproveAsync(string resourceName, string resourceType, CancellationToken cancellationToken)
        {
            if (_index >= decisions.Count)
            {
                throw new InvalidOperationException($"ScriptedApprover ran out of decisions at resource '{resourceName}'.");
            }

            return Task.FromResult(decisions[_index++]);
        }
    }
}
