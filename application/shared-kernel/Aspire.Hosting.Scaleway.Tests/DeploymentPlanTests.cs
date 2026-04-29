using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class DeploymentPlanTests
{
    [Fact]
    public void WithMonthlyBudget_SetsBudgetOnEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        var environment = builder.AddScalewayEnvironment("staging")
            .WithMonthlyBudget(50m);

        environment.Resource.MonthlyBudget.Should().Be(50m);
    }

    [Fact]
    public void BudgetCheck_WhenUnderBudget_ShouldPass()
    {
        var check = new BudgetCheckResult(100m, 73.40m, "EUR");

        check.ExceedsBudget.Should().BeFalse();
        check.Message.Should().Contain("within budget");
    }

    [Fact]
    public void BudgetCheck_WhenOverBudget_ShouldFail()
    {
        var check = new BudgetCheckResult(50m, 73.40m, "EUR");

        check.ExceedsBudget.Should().BeTrue();
        check.Message.Should().Contain("exceeds budget");
        check.Message.Should().Contain("73.40");
        check.Message.Should().Contain("50.00");
    }

    [Fact]
    public void DeploymentPlan_WhenNoBlockedChangesAndUnderBudget_CanDeploy()
    {
        var plan = new DeploymentPlan(
            [new DeploymentChange("my-db", DeploymentChangeType.Create, DeploymentChangeSeverity.Safe, "Create rdb")],
            new DeploymentCostSummary([new CostEstimate("my-db", "DB-DEV-S", 8.76m, "EUR", "DB-DEV-S")], 8.76m, "EUR"),
            new BudgetCheckResult(50m, 8.76m, "EUR")
        );

        plan.CanDeploy.Should().BeTrue();
        plan.HasBlockedChanges.Should().BeFalse();
        plan.ExceedsBudget.Should().BeFalse();
    }

    [Fact]
    public void DeploymentPlan_WhenBlockedChanges_CannotDeploy()
    {
        var plan = new DeploymentPlan(
            [new DeploymentChange("my-db", DeploymentChangeType.Update, DeploymentChangeSeverity.Blocked, "Region change blocked")],
            new DeploymentCostSummary([], 0m, "EUR"),
            null
        );

        plan.CanDeploy.Should().BeFalse();
        plan.HasBlockedChanges.Should().BeTrue();
    }

    [Fact]
    public void DeploymentPlan_WhenOverBudget_CannotDeploy()
    {
        var plan = new DeploymentPlan(
            [new DeploymentChange("my-db", DeploymentChangeType.Create, DeploymentChangeSeverity.Safe, "Create rdb")],
            new DeploymentCostSummary([new CostEstimate("my-db", "DB-GP-XL", 200m, "EUR", "DB-GP-XL")], 200m, "EUR"),
            new BudgetCheckResult(50m, 200m, "EUR")
        );

        plan.CanDeploy.Should().BeFalse();
        plan.ExceedsBudget.Should().BeTrue();
        plan.HasBlockedChanges.Should().BeFalse();
    }

    [Fact]
    public void DeploymentPlan_WhenNoBudgetSet_CanDeploy()
    {
        var plan = new DeploymentPlan(
            [new DeploymentChange("my-db", DeploymentChangeType.Create, DeploymentChangeSeverity.Safe, "Create rdb")],
            new DeploymentCostSummary([new CostEstimate("my-db", "DB-GP-XL", 200m, "EUR", "DB-GP-XL")], 200m, "EUR"),
            null
        );

        plan.CanDeploy.Should().BeTrue();
        plan.ExceedsBudget.Should().BeFalse();
    }

    [Fact]
    public void BudgetCheck_WhenExactlyOnBudget_ShouldPass()
    {
        var check = new BudgetCheckResult(50m, 50m, "EUR");

        check.ExceedsBudget.Should().BeFalse();
    }
}
