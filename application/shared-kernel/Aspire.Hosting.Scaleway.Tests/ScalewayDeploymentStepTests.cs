using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayDeploymentStepTests
{
    [Fact]
    public async Task DeployAsync_ShouldCreatePrivateNetworkAndRegistry()
    {
        // Arrange
        var createdResources = new List<string>();
        var apiClient = CreateMockApiClient((method, url, _) =>
            {
                if (method == "GET") return EmptyListResponse(url);
                createdResources.Add(ExtractResourceType(url));
                return ResourceResponse("new-id");
            }
        );

        var environment = CreateEnvironment("production");

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [], apiClient);

        // Assert
        createdResources.Should().Contain("private-networks");
        createdResources.Should().Contain("namespaces");
    }

    [Fact]
    public async Task DeployAsync_ShouldProvisionRdbWithPrivateNetwork()
    {
        // Arrange
        var postBodies = new List<string>();
        var apiClient = CreateMockApiClient((method, url, body) =>
            {
                if (method == "POST") postBodies.Add(body ?? "");
                if (method == "GET") return EmptyListResponse(url);
                return ResourceResponse("new-id");
            }
        );

        var environment = CreateEnvironment("staging");
        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation { Config = new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" } });

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], apiClient);

        // Assert - should have created private network, registry, and RDB
        postBodies.Should().HaveCountGreaterOrEqualTo(3);
        var rdbBody = postBodies.First(b => b.Contains("PostgreSQL-16"));
        rdbBody.Should().Contain("DB-DEV-S");
        rdbBody.Should().Contain("private_network_id");
    }

    [Fact]
    public async Task DeployAsync_WhenResourceExists_ShouldSkipCreation()
    {
        // Arrange
        var postCount = 0;
        var apiClient = CreateMockApiClient((method, url, _) =>
            {
                if (method == "POST") postCount++;
                if (method == "GET" && url.Contains("instances")) return ListResponse("instances", "existing-rdb", "my-db");
                if (method == "GET") return EmptyListResponse(url);
                return ResourceResponse("new-id");
            }
        );

        var environment = CreateEnvironment("staging");
        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation());

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], apiClient);

        // Assert - should create private network + registry but NOT the RDB (it exists)
        postCount.Should().Be(2);
    }

    [Fact]
    public async Task DryRunAsync_WhenNothingExists_ShouldPlanAllCreates()
    {
        // Arrange
        var apiClient = CreateMockApiClient((method, _, _) =>
            {
                if (method == "GET")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"items": [], "total_count": 0, "private_networks": [], "namespaces": [], "instances": [], "clusters": [], "containers": []}""", Encoding.UTF8, "application/json")
                    };
                }

                return ResourceResponse("new-id");
            }
        );

        var environment = CreateEnvironment("staging");
        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation());
        var redis = new ScalewayRedisClusterResource("my-cache");
        redis.Annotations.Add(new PublishAsScalewayRedisAnnotation());

        // Act
        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, [rdb, redis], apiClient);

        // Assert
        changes.Should().Contain(c => c.ResourceName == "staging-network" && c.ChangeType == DeploymentChangeType.Create);
        changes.Should().Contain(c => c.ResourceName == "staging-registry" && c.ChangeType == DeploymentChangeType.Create);
        changes.Should().Contain(c => c.ResourceName == "my-db" && c.ChangeType == DeploymentChangeType.Create);
        changes.Should().Contain(c => c.ResourceName == "my-cache" && c.ChangeType == DeploymentChangeType.Create);
        changes.Where(c => c.IsBlocked).Should().BeEmpty();
    }

    [Fact]
    public async Task DryRunAsync_WhenEverythingExists_ShouldPlanNoChanges()
    {
        // Arrange
        var apiClient = CreateMockApiClient((_, url, _) =>
            {
                if (url.Contains("private-networks")) return ListResponse("private_networks", "net-1", "staging-network");
                if (url.Contains("registry")) return ListResponse("namespaces", "reg-1", "staging-registry");
                if (url.Contains("rdb") && url.Contains("instances")) return RdbInstanceResponse("rdb-1", "fr-par", "PostgreSQL-16", "DB-DEV-S", false);
                return EmptyListResponse(url);
            }
        );

        var environment = CreateEnvironment("staging");
        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation { Config = new ScalewayRdbPublishConfig { Region = ScalewayRegion.FrPar, Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" } });

        // Act
        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, [rdb], apiClient);

        // Assert
        changes.Where(c => c.ChangeType == DeploymentChangeType.Create).Should().BeEmpty();
        changes.Where(c => c.IsBlocked).Should().BeEmpty();
    }

    [Fact]
    public async Task DryRunAsync_WhenRdbRegionChanged_ShouldBlockChange()
    {
        // Arrange
        var apiClient = CreateMockApiClient((_, url, _) =>
            {
                if (url.Contains("private-networks")) return ListResponse("private_networks", "net-1", "staging-network");
                if (url.Contains("registry")) return ListResponse("namespaces", "reg-1", "staging-registry");
                if (url.Contains("rdb") && url.Contains("instances")) return RdbInstanceResponse("rdb-1", "fr-par", "PostgreSQL-16", "DB-DEV-S", false);
                return EmptyListResponse(url);
            }
        );

        var environment = CreateEnvironment("staging");
        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation { Config = new ScalewayRdbPublishConfig { Region = ScalewayRegion.NlAms, Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" } });

        // Act
        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, [rdb], apiClient);

        // Assert
        changes.Should().Contain(c => c.IsBlocked && c.Description.Contains("region"));
    }

    [Fact]
    public async Task DryRunAsync_ShouldNotMakeAnyPostRequests()
    {
        // Arrange
        var postCount = 0;
        var apiClient = CreateMockApiClient((method, _, _) =>
            {
                if (method == "POST") postCount++;
                return EmptyListResponse("");
            }
        );

        var environment = CreateEnvironment("staging");
        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation());

        // Act
        await ScalewayDeploymentStep.DryRunAsync(environment, [rdb], apiClient);

        // Assert
        postCount.Should().Be(0);
    }

    private static ScalewayEnvironmentResource CreateEnvironment(string name)
    {
        var config = new ScalewayCredentialConfig
        {
            SecretKey = "test-key",
            DefaultProjectId = "test-project",
            DefaultRegion = ScalewayRegion.FrPar
        };
        return new ScalewayEnvironmentResource(name, config, true);
    }

    private static ScalewayApiClient CreateMockApiClient(Func<string, string, string?, HttpResponseMessage> handler)
    {
        var messageHandler = new MockHandler(handler);
        var httpClient = new HttpClient(messageHandler) { BaseAddress = new Uri("https://api.scaleway.com") };
        return new ScalewayApiClient(httpClient);
    }

    private static string ExtractResourceType(string url)
    {
        var segments = url.Split('/');
        return segments.Last(s => !string.IsNullOrEmpty(s) && !s.Contains('?') && !s.Contains("v1") && !s.Contains("v2") && !s.Contains("regions") && !s.Contains("zones") && !s.StartsWith("fr-") && !s.StartsWith("nl-") && !s.StartsWith("pl-"));
    }

    private static HttpResponseMessage EmptyListResponse(string url)
    {
        var arrayName = url.Contains("private-networks") ? "private_networks"
            : url.Contains("instances") ? "instances"
            : url.Contains("clusters") ? "clusters"
            : url.Contains("containers") && !url.Contains("namespaces") ? "containers"
            : "namespaces";
        var json = $$"""{"{{arrayName}}": [], "total_count": 0}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage ListResponse(string arrayName, string id, string name = "test")
    {
        var json = $$"""{"{{arrayName}}": [{"id": "{{id}}", "name": "{{name}}", "status": "ready"}], "total_count": 1}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage RdbInstanceResponse(string id, string region, string engine, string nodeType, bool isHaCluster)
    {
        var json = $$"""{"instances": [{"id": "{{id}}", "name": "my-db", "region": "{{region}}", "engine": "{{engine}}", "node_type": "{{nodeType}}", "is_ha_cluster": {{(isHaCluster ? "true" : "false")}}, "status": "ready"}], "total_count": 1}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage ResourceResponse(string id)
    {
        var json = JsonSerializer.Serialize(new { id, name = "test", status = "provisioning" });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class MockHandler(Func<string, string, string?, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            return handler(request.Method.Method, request.RequestUri!.PathAndQuery, body);
        }
    }
}
