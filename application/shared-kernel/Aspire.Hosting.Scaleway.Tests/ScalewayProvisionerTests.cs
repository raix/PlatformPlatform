using System.Net;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayProvisionerTests
{
    [Fact]
    public async Task ProvisionRdbInstance_WhenNoExistingResource_ShouldCreateNew()
    {
        // Arrange
        var capturedRequests = new List<(string Method, string Url, string? Body)>();
        var httpClient = CreateMockHttpClient((method, url, body) =>
        {
            capturedRequests.Add((method, url, body));

            if (method == "GET")
            {
                return CreateListResponse("instances", []);
            }

            return CreateResourceResponse("rdb-instance-123");
        });

        var provisioner = new ScalewayProvisioner(httpClient, "my-app", "project-123");
        var config = new ScalewayRdbPublishConfig { Engine = "PostgreSQL-16", NodeType = "DB-DEV-S" };

        // Act
        var result = await provisioner.ProvisionRdbInstanceAsync("my-db", config);

        // Assert
        result.ResourceId.Should().Be("rdb-instance-123");
        capturedRequests.Should().HaveCount(2);
        capturedRequests[0].Method.Should().Be("GET");
        capturedRequests[0].Url.Should().Contain("/rdb/v1/regions/fr-par/instances");
        capturedRequests[1].Method.Should().Be("POST");
        capturedRequests[1].Body.Should().Contain("PostgreSQL-16");
        capturedRequests[1].Body.Should().Contain("DB-DEV-S");
        capturedRequests[1].Body.Should().Contain("project-123");
    }

    [Fact]
    public async Task ProvisionRdbInstance_WhenExistingResourceFound_ShouldReturnExisting()
    {
        // Arrange
        var httpClient = CreateMockHttpClient((method, url, _) =>
        {
            if (method == "GET")
            {
                return CreateListResponse("instances", [CreateResourceJson("existing-rdb-456")]);
            }

            throw new InvalidOperationException("Should not create when resource exists.");
        });

        var provisioner = new ScalewayProvisioner(httpClient, "my-app", "project-123");
        var config = new ScalewayRdbPublishConfig();

        // Act
        var result = await provisioner.ProvisionRdbInstanceAsync("my-db", config);

        // Assert
        result.ResourceId.Should().Be("existing-rdb-456");
    }

    [Fact]
    public async Task ProvisionRedisCluster_WhenNoExistingResource_ShouldCreateNew()
    {
        // Arrange
        var capturedRequests = new List<(string Method, string Url, string? Body)>();
        var httpClient = CreateMockHttpClient((method, url, body) =>
        {
            capturedRequests.Add((method, url, body));

            if (method == "GET")
            {
                return CreateListResponse("clusters", []);
            }

            return CreateResourceResponse("redis-cluster-789");
        });

        var provisioner = new ScalewayProvisioner(httpClient, "my-app", "project-123");
        var config = new ScalewayRedisPublishConfig { Version = "7.0", NodeType = "RED1-MICRO", ClusterSize = 3 };

        // Act
        var result = await provisioner.ProvisionRedisClusterAsync("my-cache", config);

        // Assert
        result.ResourceId.Should().Be("redis-cluster-789");
        capturedRequests[1].Method.Should().Be("POST");
        capturedRequests[1].Url.Should().Contain("/redis/v1/zones/fr-par-1/clusters");
        capturedRequests[1].Body.Should().Contain("RED1-MICRO");
    }

    [Fact]
    public async Task ProvisionRdbInstance_ShouldIncludeAspireTags()
    {
        // Arrange
        string? capturedBody = null;
        var httpClient = CreateMockHttpClient((method, _, body) =>
        {
            if (method == "POST")
            {
                capturedBody = body;
            }
            return method == "GET" ? CreateListResponse("instances", []) : CreateResourceResponse("new-id");
        });

        var provisioner = new ScalewayProvisioner(httpClient, "platform-platform", "project-123");
        var config = new ScalewayRdbPublishConfig();

        // Act
        await provisioner.ProvisionRdbInstanceAsync("account-db", config);

        // Assert
        capturedBody.Should().Contain("aspire-app=platform-platform");
        capturedBody.Should().Contain("aspire-resource=account-db");
    }

    [Fact]
    public void Constructor_WhenMissingProjectId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var credentials = new ScalewayCredentialConfig
        {
            AccessKey = "SCW-ACCESS-KEY",
            SecretKey = "SCW-SECRET-KEY",
            DefaultProjectId = null
        };

        // Act
        var act = () => new ScalewayProvisioner(credentials, "my-app");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SCW_DEFAULT_PROJECT_ID*");
    }

    [Fact]
    public async Task ProvisionRdbInstance_ShouldUseConfiguredRegion()
    {
        // Arrange
        var capturedUrls = new List<string>();
        var httpClient = CreateMockHttpClient((method, url, _) =>
        {
            capturedUrls.Add(url);
            return method == "GET" ? CreateListResponse("instances", []) : CreateResourceResponse("new-id");
        });

        var provisioner = new ScalewayProvisioner(httpClient, "my-app", "project-123");
        var config = new ScalewayRdbPublishConfig { Region = ScalewayRegion.NlAms };

        // Act
        await provisioner.ProvisionRdbInstanceAsync("my-db", config);

        // Assert
        capturedUrls.Should().AllSatisfy(url => url.Should().Contain("nl-ams"));
    }

    private static HttpClient CreateMockHttpClient(Func<string, string, string?, HttpResponseMessage> handler)
    {
        var messageHandler = new MockHttpMessageHandler(handler);
        return new HttpClient(messageHandler) { BaseAddress = new Uri("https://api.scaleway.com") };
    }

    private static HttpResponseMessage CreateListResponse(string propertyName, JsonElement[] items)
    {
        var json = $"{{\"{propertyName}\": [{string.Join(",", items.Select(i => i.GetRawText()))}], \"total_count\": {items.Length}}}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static JsonElement CreateResourceJson(string id)
    {
        var json = JsonSerializer.Serialize(new { id, name = "test", status = "ready" });
        return JsonDocument.Parse(json).RootElement;
    }

    private static HttpResponseMessage CreateResourceResponse(string id)
    {
        var json = JsonSerializer.Serialize(new { id, name = "test", status = "provisioning" });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class MockHttpMessageHandler(Func<string, string, string?, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            return handler(request.Method.Method, request.RequestUri!.PathAndQuery, body);
        }
    }
}
