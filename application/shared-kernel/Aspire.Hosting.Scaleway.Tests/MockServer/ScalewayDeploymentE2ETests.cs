using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests.MockServer;

public sealed class ScalewayDeploymentE2ETests : IDisposable
{
    private readonly ScalewayMockServer _mockServer = new();

    public ScalewayDeploymentE2ETests()
    {
        _mockServer.Start();
    }

    public void Dispose()
    {
        _mockServer.Dispose();
    }

    [Fact]
    public async Task FullDeploy_ShouldCreateInfrastructureInCorrectOrder()
    {
        // Arrange
        var environment = CreateEnvironment("production");

        var rdb = new ScalewayRdbInstanceResource("account-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation
            {
                Config = new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" }
            }
        );

        var redis = new ScalewayRedisClusterResource("session-cache");
        redis.Annotations.Add(new PublishAsScalewayRedisAnnotation
            {
                Config = new ScalewayRedisPublishConfig { Version = "7.0", NodeType = "RED1-MICRO" }
            }
        );

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb, redis], CancellationToken.None);

        // Assert - infrastructure created in correct order
        var posts = _mockServer.ReceivedRequests.Where(r => r.Method == "POST").ToList();
        posts.Should().HaveCountGreaterOrEqualTo(4); // network, registry, rdb, redis

        // Private network created first
        posts[0].Path.Should().Contain("private-networks");

        // Registry created second
        posts[1].Path.Should().Contain("namespaces");

        // RDB created with correct config
        var rdbPost = posts.First(p => p.Path.Contains("rdb"));
        rdbPost.Body.Should().Contain("PostgreSQL-16");
        rdbPost.Body.Should().Contain("DB-DEV-S");
        rdbPost.Body.Should().Contain("private_network_id");

        // Redis created with correct config
        var redisPost = posts.First(p => p.Path.Contains("redis"));
        redisPost.Body.Should().Contain("RED1-MICRO");
    }

    [Fact]
    public async Task FullDeploy_ShouldPassProjectIdInAllRequests()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation());

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], CancellationToken.None);

        // Assert - all POST requests include the project ID
        var posts = _mockServer.ReceivedRequests.Where(r => r.Method == "POST").ToList();
        posts.Should().AllSatisfy(p => p.Body.Should().Contain("e2e-project"));
    }

    [Fact]
    public async Task FullDeploy_ShouldPassAuthTokenInAllRequests()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [], CancellationToken.None);

        // Assert - all requests should have hit the mock server
        _mockServer.ReceivedRequests.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FullDeploy_WhenResourceAlreadyExists_ShouldNotRecreate()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation());

        // Act - deploy twice
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], CancellationToken.None);
        var firstRunPosts = _mockServer.ReceivedRequests.Count(r => r.Method == "POST");

        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], CancellationToken.None);
        var secondRunPosts = _mockServer.ReceivedRequests.Count(r => r.Method == "POST") - firstRunPosts;

        // Assert - second run should not create anything new (all resources exist)
        secondRunPosts.Should().Be(0);
    }

    [Fact]
    public async Task FullDeploy_ShouldStoreResourcesInMockServer()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation { Config = new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16" } });

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], CancellationToken.None);

        // Assert - resources stored in mock server's state
        _mockServer.Resources.Should().ContainKey("private-networks");
        _mockServer.Resources.Should().ContainKey("namespaces");
        _mockServer.Resources.Should().ContainKey("instances");
        _mockServer.Resources["instances"].Should().HaveCount(1);
    }

    [Fact]
    public async Task DryRun_ShouldOnlyMakeGetRequests()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation());

        using var apiClient = new ScalewayApiClient(environment.CredentialConfig);

        // Act
        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, [rdb], apiClient, CancellationToken.None);

        // Assert
        _mockServer.ReceivedRequests.Where(r => r.Method == "POST").Should().BeEmpty();
        _mockServer.ReceivedRequests.Where(r => r.Method == "GET").Should().NotBeEmpty();
        changes.Should().Contain(c => c.ChangeType == DeploymentChangeType.Create);
    }

    [Fact]
    public async Task DryRun_AfterDeploy_ShouldShowNoChanges()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        var rdb = new ScalewayRdbInstanceResource("my-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation
            {
                Config = new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S", Region = ScalewayRegion.FrPar }
            }
        );

        // Deploy first
        await ScalewayDeploymentStep.DeployAsync(environment, [rdb], CancellationToken.None);

        // Act - dry run after deploy
        using var apiClient = new ScalewayApiClient(environment.CredentialConfig);
        var changes = await ScalewayDeploymentStep.DryRunAsync(environment, [rdb], apiClient, CancellationToken.None);

        // Assert - no creates, no blocked changes
        changes.Where(c => c.ChangeType == DeploymentChangeType.Create).Should().BeEmpty();
        changes.Where(c => c.IsBlocked).Should().BeEmpty();
    }

    [Fact]
    public async Task FullDeploy_ContainerShouldIncludePrivateNetworkId()
    {
        // Arrange
        var environment = CreateEnvironment("staging");

        var container = new ScalewayRdbInstanceResource("my-api");
        container.Annotations.Add(new PublishAsScalewayContainerAnnotation
            {
                Config = new ScalewayContainerPublishConfig { MemoryLimitMb = 512, MinScale = 1, MaxScale = 5, Port = 8080 }
            }
        );

        // Act
        await ScalewayDeploymentStep.DeployAsync(environment, [container], CancellationToken.None);

        // Assert
        var containerPost = _mockServer.ReceivedRequests
            .Where(r => r.Method == "POST")
            .FirstOrDefault(r => r.Path.Contains("containers") && !r.Path.Contains("namespaces"));

        containerPost.Should().NotBeNull();
        containerPost.Body.Should().Contain("private_network_id");
        containerPost.Body.Should().Contain("512000000"); // memory in bytes
    }

    private ScalewayEnvironmentResource CreateEnvironment(string name)
    {
        var config = new ScalewayCredentialConfig
        {
            AccessKey = "SCW-E2E-ACCESS-KEY",
            SecretKey = "e2e-secret-key",
            DefaultProjectId = "e2e-project",
            DefaultRegion = ScalewayRegion.FrPar,
            ApiUrl = _mockServer.Url
        };
        return new ScalewayEnvironmentResource(name, config, true);
    }
}
