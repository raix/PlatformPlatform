using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewaySecretClientTests
{
    [Fact]
    public async Task GetOrCreateAsync_WhenSecretMissing_CreatesAndWritesVersion()
    {
        // Arrange
        var requests = new List<(string Method, string Path, string? Body)>();
        var apiClient = CreateMockApiClient((method, path, body) =>
            {
                requests.Add((method, path, body));

                if (method == "GET" && path.Contains("/secrets?"))
                {
                    return EmptyListResponse("secrets");
                }

                if (method == "POST" && path.Contains("/secrets") && !path.Contains("/versions"))
                {
                    return JsonResponse(new { id = "secret-1", name = "rdb-test-password" });
                }

                if (method == "POST" && path.Contains("/versions"))
                {
                    return JsonResponse(new { id = "version-1" });
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        );
        var client = new ScalewaySecretClient(apiClient);

        // Act
        var result = await client.GetOrCreateAsync(
            "project-1", "fr-par", "rdb-test-password",
            () => "generated-password",
            ["aspire-managed"]
        );

        // Assert
        result.Should().Be("generated-password");
        requests.Should().Contain(r => r.Method == "POST" && r.Path.Contains("/secrets") && !r.Path.Contains("/versions"));
        requests.Should().Contain(r => r.Method == "POST" && r.Path.Contains("/versions"));
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenSecretExists_ReturnsExistingValueWithoutCreating()
    {
        // Arrange
        var posts = new List<string>();
        var apiClient = CreateMockApiClient((method, path, _) =>
            {
                if (method == "POST")
                {
                    posts.Add(path);
                }

                if (method == "GET" && path.Contains("/secrets?"))
                {
                    return JsonResponse(new
                        {
                            secrets = new[] { new { id = "secret-1", name = "rdb-test-password" } },
                            total_count = 1
                        }
                    );
                }

                if (method == "GET" && path.Contains("/versions/latest_enabled/access"))
                {
                    var base64 = Convert.ToBase64String("existing-password"u8);
                    return JsonResponse(new { data = base64 });
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        );
        var client = new ScalewaySecretClient(apiClient);
        var factoryCalled = false;

        // Act
        var result = await client.GetOrCreateAsync(
            "project-1", "fr-par", "rdb-test-password",
            () =>
            {
                factoryCalled = true;
                return "should-not-be-used";
            },
            ["aspire-managed"]
        );

        // Assert
        result.Should().Be("existing-password");
        factoryCalled.Should().BeFalse("the factory must not run when a secret already exists");
        posts.Should().BeEmpty("no POSTs should be made when the secret already exists");
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenOrphanedSecretExistsButValueAccessFails_RegeneratesValue()
    {
        // Simulates the partial-failure recovery: the secret resource exists in SM but
        // its latest version is empty/disabled. Re-deploy must add a fresh version rather than fail.

        // Arrange
        var versionPosts = new List<string>();
        var apiClient = CreateMockApiClient((method, path, body) =>
            {
                if (method == "POST" && path.Contains("/versions"))
                {
                    versionPosts.Add(body!);
                    return JsonResponse(new { id = "version-2" });
                }

                if (method == "GET" && path.Contains("/secrets?"))
                {
                    return JsonResponse(new
                        {
                            secrets = new[] { new { id = "secret-1", name = "rdb-test-password" } },
                            total_count = 1
                        }
                    );
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound); // access endpoint 404s
            }
        );
        var client = new ScalewaySecretClient(apiClient);

        // Act
        var result = await client.GetOrCreateAsync(
            "project-1", "fr-par", "rdb-test-password",
            () => "fresh-password",
            ["aspire-managed"]
        );

        // Assert
        result.Should().Be("fresh-password");
        versionPosts.Should().HaveCount(1);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(JsonDocument.Parse(versionPosts[0]).RootElement.GetProperty("data").GetString()!));
        decoded.Should().Be("fresh-password");
    }

    [Fact]
    public async Task SetAsync_WhenSecretMissing_CreatesAndWritesVersion()
    {
        // Arrange
        var requests = new List<(string Method, string Path, string? Body)>();
        var apiClient = CreateMockApiClient((method, path, body) =>
            {
                requests.Add((method, path, body));

                if (method == "GET" && path.Contains("/secrets?")) return EmptyListResponse("secrets");
                if (method == "POST" && path.Contains("/secrets") && !path.Contains("/versions")) return JsonResponse(new { id = "secret-1" });
                if (method == "POST" && path.Contains("/versions")) return JsonResponse(new { id = "version-1" });
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        );
        var client = new ScalewaySecretClient(apiClient);

        // Act
        await client.SetAsync("project-1", "fr-par", "rdb-test-host", "10.0.0.5", ["aspire-managed"]);

        // Assert
        requests.Where(r => r.Method == "POST").Should().HaveCount(2);
        var versionPost = requests.First(r => r.Method == "POST" && r.Path.Contains("/versions")).Body!;
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(JsonDocument.Parse(versionPost).RootElement.GetProperty("data").GetString()!));
        decoded.Should().Be("10.0.0.5");
    }

    [Fact]
    public async Task SetAsync_WhenSecretExists_AddsNewVersionWithoutRecreatingSecret()
    {
        // Arrange
        var posts = new List<string>();
        var apiClient = CreateMockApiClient((method, path, _) =>
            {
                if (method == "POST") posts.Add(path);

                if (method == "GET" && path.Contains("/secrets?"))
                {
                    return JsonResponse(new
                        {
                            secrets = new[] { new { id = "secret-1", name = "rdb-test-host" } },
                            total_count = 1
                        }
                    );
                }

                if (method == "POST" && path.Contains("/versions")) return JsonResponse(new { id = "version-2" });
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        );
        var client = new ScalewaySecretClient(apiClient);

        // Act
        await client.SetAsync("project-1", "fr-par", "rdb-test-host", "10.0.0.5", ["aspire-managed"]);

        // Assert
        posts.Should().HaveCount(1, "only the version POST — the secret already exists");
        posts[0].Should().Contain("/versions");
    }

    private static ScalewayApiClient CreateMockApiClient(Func<string, string, string?, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new MockHandler(handler)) { BaseAddress = new Uri("https://api.scaleway.com") };
        return new ScalewayApiClient(httpClient);
    }

    private static HttpResponseMessage EmptyListResponse(string arrayName)
    {
        var json = $"{{\"{arrayName}\":[],\"total_count\":0}}";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage JsonResponse(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
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
