using Aspire.Hosting.Scaleway.Tests.MockServer;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests.E2E;

/// <summary>
///     End-to-end tests that spawn <c>aspire deploy</c> as a subprocess against a per-test
///     Scaleway mock server. Tagged with Category=E2E so they can be excluded from fast runs.
/// </summary>
[Trait("Category", "E2E")]
public sealed class DeployScenarioTests
{
    [Fact]
    public async Task Deploy_AgainstCleanScaleway_ProvisionsSharedInfrastructureAndPublishTargets()
    {
        using var mockServer = new ScalewayMockServer();
        mockServer.Start();

        var result = await AppHostRunner.RunDeployAsync(mockServer.Url);

        result.ExitCode.Should().Be(0, "deploy should succeed when Scaleway is empty.\n{0}", result.CombinedOutput);

        var posts = mockServer.ReceivedRequests.Where(r => r.Method == "POST").ToList();
        posts.Should().Contain(r => r.Path.Contains("private-networks"), "private network must be created first");
        posts.Should().Contain(r => r.Path.Contains("registry/v1") && r.Path.Contains("namespaces"), "registry namespace must be created");
        posts.Should().Contain(r => r.Path.Contains("rdb/v1") && r.Path.Contains("instances"), "RDB instance must be created");
        posts.Should().Contain(r => r.Path.Contains("containers/v1beta1") && r.Path.Contains("namespaces"), "container namespace must be created");
        posts.Should().Contain(r => r.Path.Contains("containers/v1beta1") && r.Path.Contains("/containers"), "at least one serverless container must be created");
    }

    [Fact]
    public async Task Deploy_WhenAlreadyProvisioned_IsIdempotent()
    {
        using var mockServer = new ScalewayMockServer();
        mockServer.Start();

        // Pre-seed the mock with everything the AppHost would create — same names, matching engine.
        SeedFullDeploymentState(mockServer);

        var result = await AppHostRunner.RunDeployAsync(mockServer.Url);

        result.ExitCode.Should().Be(0, "deploy against a fully-provisioned Scaleway should be a no-op.\n{0}", result.CombinedOutput);
        mockServer.ReceivedRequests.Where(r => r.Method == "POST").Should().BeEmpty("nothing should be created when all desired state already exists");
    }

    [Fact]
    public async Task Deploy_WhenRdbExistsWithDifferentEngine_AbortsBeforeAnyChanges()
    {
        using var mockServer = new ScalewayMockServer();
        mockServer.Start();

        // Pre-seed an RDB with engine PostgreSQL-15; AppHost desires PostgreSQL-16.
        // Engine is an immutable field, so the planner produces a Blocked change.
        mockServer.Seed("instances", new
            {
                id = "rdb-existing",
                name = "postgres",
                engine = "PostgreSQL-15",
                node_type = "DB-DEV-S",
                region = "fr-par",
                status = "ready"
            }
        );

        var result = await AppHostRunner.RunDeployAsync(mockServer.Url);

        result.ExitCode.Should().NotBe(0, "deploy must abort when the plan contains blocked changes.\n{0}", result.CombinedOutput);
        result.CombinedOutput.Should().Contain("blocked changes", "the abort message should explain why");
        mockServer.ReceivedRequests.Where(r => r.Method == "POST").Should().BeEmpty("no POSTs should be issued when the plan is blocked");
    }

    private static void SeedFullDeploymentState(ScalewayMockServer mockServer)
    {
        mockServer.Seed("private-networks", new { id = "pn-1", name = "scaleway-network", region = "fr-par" });
        // Both registry and container namespaces are stored under the "namespaces" bucket;
        // the mock filters by the `name` query param, so seeding distinct entries for each is required.
        mockServer.Seed("namespaces", new { id = "ns-registry", name = "scaleway-registry", region = "fr-par" });
        mockServer.Seed("namespaces", new { id = "ns-container", name = "default", region = "fr-par" });
        mockServer.Seed("instances", new
            {
                id = "rdb-1",
                name = "postgres",
                engine = "PostgreSQL-16",
                node_type = "DB-DEV-S",
                region = "fr-par"
            }
        );
        mockServer.Seed("containers", new { id = "c-1", name = "account-api", region = "fr-par" });
        mockServer.Seed("containers", new { id = "c-2", name = "account-workers", region = "fr-par" });
        mockServer.Seed("containers", new { id = "c-3", name = "back-office-api", region = "fr-par" });
        mockServer.Seed("containers", new { id = "c-4", name = "back-office-workers", region = "fr-par" });
        mockServer.Seed("containers", new { id = "c-5", name = "main-api", region = "fr-par" });
        mockServer.Seed("containers", new { id = "c-6", name = "main-workers", region = "fr-par" });
        mockServer.Seed("containers", new { id = "c-7", name = "app-gateway", region = "fr-par" });
    }
}
