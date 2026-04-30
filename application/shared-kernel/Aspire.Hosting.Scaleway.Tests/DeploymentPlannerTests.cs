using System.Text.Json;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class DeploymentPlannerTests
{
    private readonly DeploymentPlanner _planner = new();

    [Fact]
    public void PlanCreate_ShouldReturnSafeChange()
    {
        var change = _planner.PlanCreate("my-db", "rdb");

        change.ChangeType.Should().Be(DeploymentChangeType.Create);
        change.Severity.Should().Be(DeploymentChangeSeverity.Safe);
        change.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void PlanDelete_WhenRetainPolicy_ShouldRetainResource()
    {
        var change = _planner.PlanDelete("my-db", "rdb", DeletionPolicy.Retain);

        change.ChangeType.Should().Be(DeploymentChangeType.NoChange);
        change.Severity.Should().Be(DeploymentChangeSeverity.Safe);
        change.Description.Should().Contain("retained");
    }

    [Fact]
    public void PlanDelete_WhenDeletePolicyOnDataResource_ShouldBeBlocked()
    {
        // Data-bearing resources with default Retain policy should block deletion
        var change = _planner.PlanDelete("my-db", "rdb", DeletionPolicy.Delete);

        change.ChangeType.Should().Be(DeploymentChangeType.Delete);
        change.Severity.Should().Be(DeploymentChangeSeverity.Blocked);
        change.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void PlanDelete_WhenDeletePolicyOnStatelessResource_ShouldBeSafe()
    {
        var change = _planner.PlanDelete("my-api", "container", DeletionPolicy.Delete);

        change.ChangeType.Should().Be(DeploymentChangeType.Delete);
        change.Severity.Should().Be(DeploymentChangeSeverity.Safe);
        change.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void PlanUpdate_WhenImmutableFieldChanges_ShouldBeBlocked()
    {
        var changes = _planner.PlanUpdate("my-db", "rdb", new Dictionary<string, (string?, string?)>
            {
                ["region"] = ("fr-par", "nl-ams")
            }
        );

        changes.Should().HaveCount(1);
        changes[0].Severity.Should().Be(DeploymentChangeSeverity.Blocked);
        changes[0].IsBlocked.Should().BeTrue();
        changes[0].Description.Should().Contain("data loss");
    }

    [Fact]
    public void PlanUpdate_WhenEngineChanges_ShouldBeBlocked()
    {
        var changes = _planner.PlanUpdate("my-db", "rdb", new Dictionary<string, (string?, string?)>
            {
                ["engine"] = ("PostgreSQL-15", "PostgreSQL-16")
            }
        );

        changes.Should().HaveCount(1);
        changes[0].Severity.Should().Be(DeploymentChangeSeverity.Blocked);
    }

    [Fact]
    public void PlanUpdate_WhenNodeTypeChanges_ShouldWarn()
    {
        var changes = _planner.PlanUpdate("my-db", "rdb", new Dictionary<string, (string?, string?)>
            {
                ["node_type"] = ("DB-DEV-S", "DB-GP-S")
            }
        );

        changes.Should().HaveCount(1);
        changes[0].Severity.Should().Be(DeploymentChangeSeverity.Warning);
        changes[0].IsBlocked.Should().BeFalse();
        changes[0].Description.Should().Contain("downtime");
    }

    [Fact]
    public void PlanUpdate_WhenSafeFieldChanges_ShouldBeSafe()
    {
        var changes = _planner.PlanUpdate("my-db", "rdb", new Dictionary<string, (string?, string?)>
            {
                ["tags"] = ("old-tag", "new-tag")
            }
        );

        changes.Should().HaveCount(1);
        changes[0].Severity.Should().Be(DeploymentChangeSeverity.Safe);
    }

    [Fact]
    public void PlanUpdate_WhenNoChanges_ShouldReturnEmpty()
    {
        var changes = _planner.PlanUpdate("my-db", "rdb", new Dictionary<string, (string?, string?)>
            {
                ["region"] = ("fr-par", "fr-par"),
                ["engine"] = ("PostgreSQL-16", "PostgreSQL-16")
            }
        );

        changes.Should().BeEmpty();
    }

    [Fact]
    public void PlanUpdate_WhenMultipleChanges_ShouldReturnAll()
    {
        var changes = _planner.PlanUpdate("my-db", "rdb", new Dictionary<string, (string?, string?)>
            {
                ["region"] = ("fr-par", "nl-ams"),
                ["node_type"] = ("DB-DEV-S", "DB-GP-S"),
                ["tags"] = ("v1", "v2")
            }
        );

        changes.Should().HaveCount(3);
        changes.Count(c => c.Severity == DeploymentChangeSeverity.Blocked).Should().Be(1);
        changes.Count(c => c.Severity == DeploymentChangeSeverity.Warning).Should().Be(1);
        changes.Count(c => c.Severity == DeploymentChangeSeverity.Safe).Should().Be(1);
    }

    [Fact]
    public void PlanRdbUpdate_ShouldCompareAgainstApiResponse()
    {
        var existing = JsonDocument.Parse("""
                                          {
                                              "id": "rdb-123",
                                              "region": "fr-par",
                                              "engine": "PostgreSQL-16",
                                              "node_type": "DB-DEV-S",
                                              "is_ha_cluster": false
                                          }
                                          """
        ).RootElement;

        var desired = new ScalewayRdbPublishConfig
        {
            Region = ScalewayRegion.FrPar,
            Engine = "PostgreSQL-16",
            NodeType = "DB-GP-S",
            IsHaCluster = true
        };

        var changes = _planner.PlanRdbUpdate("my-db", desired, existing);

        changes.Should().HaveCount(2);
        changes.Should().Contain(c => c.Description.Contains("node_type") && c.Severity == DeploymentChangeSeverity.Warning);
        changes.Should().Contain(c => c.Description.Contains("is_ha_cluster") && c.Severity == DeploymentChangeSeverity.Warning);
    }

    [Fact]
    public void PlanRdbUpdate_WhenRegionChanges_ShouldBlock()
    {
        var existing = JsonDocument.Parse("""{"region": "fr-par", "engine": "PostgreSQL-16", "node_type": "DB-DEV-S", "is_ha_cluster": false}""").RootElement;
        var desired = new ScalewayRdbPublishConfig { Region = ScalewayRegion.NlAms, Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" };

        var changes = _planner.PlanRdbUpdate("my-db", desired, existing);

        changes.Should().Contain(c => c.IsBlocked && c.Description.Contains("region"));
    }

    [Fact]
    public void PlanRedisUpdate_WhenZoneChanges_ShouldBlock()
    {
        var existing = JsonDocument.Parse("""{"zone": "fr-par-1", "node_type": "RED1-MICRO", "cluster_size": 1}""").RootElement;
        var desired = new ScalewayRedisPublishConfig { Zone = ScalewayZone.NlAms1, NodeType = "RED1-MICRO", ClusterSize = 1 };

        var changes = _planner.PlanRedisUpdate("my-cache", desired, existing);

        changes.Should().Contain(c => c.IsBlocked && c.Description.Contains("zone"));
    }

    [Fact]
    public void GetDefaultDeletionPolicy_DataResources_ShouldRetain()
    {
        DeploymentPlanner.GetDefaultDeletionPolicy("rdb").Should().Be(DeletionPolicy.Retain);
        DeploymentPlanner.GetDefaultDeletionPolicy("redis").Should().Be(DeletionPolicy.Retain);
        DeploymentPlanner.GetDefaultDeletionPolicy("object-storage").Should().Be(DeletionPolicy.Retain);
        DeploymentPlanner.GetDefaultDeletionPolicy("secret").Should().Be(DeletionPolicy.Retain);
    }

    [Fact]
    public void GetDefaultDeletionPolicy_StatelessResources_ShouldDelete()
    {
        DeploymentPlanner.GetDefaultDeletionPolicy("container").Should().Be(DeletionPolicy.Delete);
        DeploymentPlanner.GetDefaultDeletionPolicy("dns-record").Should().Be(DeletionPolicy.Delete);
    }

    [Fact]
    public void PlanRedisUpdate_WhenNodeTypeChanges_ShouldWarn()
    {
        // Arrange
        var existing = JsonDocument.Parse("""{"zone": "fr-par-1", "node_type": "RED1-MICRO", "cluster_size": 1}""").RootElement;
        var desired = new ScalewayRedisPublishConfig { Zone = ScalewayZone.FrPar1, NodeType = "RED1-SM", ClusterSize = 1 };

        // Act
        var changes = _planner.PlanRedisUpdate("my-cache", desired, existing);

        // Assert
        changes.Should().HaveCount(1);
        changes[0].Severity.Should().Be(DeploymentChangeSeverity.Warning);
        changes[0].IsBlocked.Should().BeFalse();
        changes[0].Description.Should().Contain("node_type");
        changes[0].Description.Should().Contain("downtime");
    }
}
