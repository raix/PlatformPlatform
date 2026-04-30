using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SharedKernel.Configuration;
using Xunit;

namespace SharedKernel.Tests.Configuration;

public sealed class ScalewaySecretManagerConfigurationProviderTests
{
    private static ScalewaySecretManagerOptions CreateOptions()
    {
        return new ScalewaySecretManagerOptions
        {
            SecretKey = "test-secret-key",
            ProjectId = "test-project-id",
            Region = "fr-par",
            ApiUrl = "https://api.scaleway.com"
        };
    }

    private static HttpClient CreateMockHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var messageHandler = new MockHttpMessageHandler(handler);
        return new HttpClient(messageHandler) { BaseAddress = new Uri("https://api.scaleway.com") };
    }

    [Fact]
    public void Load_WhenSecretsExist_ShouldPopulateConfiguration()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(request =>
            {
                if (request.RequestUri!.PathAndQuery.Contains("/secrets?"))
                {
                    return CreateListSecretsResponse([
                            ("secret-1", "database-password"),
                            ("secret-2", "jwt-signing-key")
                        ]
                    );
                }

                if (request.RequestUri.PathAndQuery.Contains("secret-1/versions/latest_enabled/access"))
                {
                    return CreateAccessSecretResponse("super-secret-password");
                }

                if (request.RequestUri.PathAndQuery.Contains("secret-2/versions/latest_enabled/access"))
                {
                    return CreateAccessSecretResponse("base64-signing-key-value");
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        );

        var provider = new ScalewaySecretManagerConfigurationProvider(CreateOptions(), httpClient);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("database-password", out var dbPassword).Should().BeTrue();
        dbPassword.Should().Be("super-secret-password");

        provider.TryGet("jwt-signing-key", out var signingKey).Should().BeTrue();
        signingKey.Should().Be("base64-signing-key-value");
    }

    [Fact]
    public void Load_WhenNoSecrets_ShouldHaveEmptyConfiguration()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"secrets": [], "total_count": 0}""", Encoding.UTF8, "application/json")
            }
        );

        var provider = new ScalewaySecretManagerConfigurationProvider(CreateOptions(), httpClient);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("anything", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_WhenApiReturnsError_ShouldHaveEmptyConfiguration()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var provider = new ScalewaySecretManagerConfigurationProvider(CreateOptions(), httpClient);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("anything", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_WhenSecretAccessFails_ShouldSkipThatSecret()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(request =>
            {
                if (request.RequestUri!.PathAndQuery.Contains("/secrets?"))
                {
                    return CreateListSecretsResponse([
                            ("secret-1", "good-secret"),
                            ("secret-2", "bad-secret")
                        ]
                    );
                }

                if (request.RequestUri.PathAndQuery.Contains("secret-1/versions/latest_enabled/access"))
                {
                    return CreateAccessSecretResponse("good-value");
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        );

        var provider = new ScalewaySecretManagerConfigurationProvider(CreateOptions(), httpClient);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("good-secret", out var value).Should().BeTrue();
        value.Should().Be("good-value");

        provider.TryGet("bad-secret", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_ShouldSendAuthTokenHeader()
    {
        // Arrange
        string? capturedAuthToken = null;
        var httpClient = CreateMockHttpClient(request =>
            {
                capturedAuthToken = request.Headers.TryGetValues("X-Auth-Token", out var values) ? values.First() : null;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"secrets": [], "total_count": 0}""", Encoding.UTF8, "application/json")
                };
            }
        );

        var options = CreateOptions();
        options.SecretKey = "my-auth-token";
        var provider = new ScalewaySecretManagerConfigurationProvider(options, httpClient);

        // Act
        provider.Load();

        // Assert
        capturedAuthToken.Should().Be("my-auth-token");
    }

    [Fact]
    public void Load_WhenTagsConfigured_ShouldIncludeTagsInRequest()
    {
        // Arrange
        string? capturedUrl = null;
        var httpClient = CreateMockHttpClient(request =>
            {
                capturedUrl = request.RequestUri!.PathAndQuery;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"secrets": [], "total_count": 0}""", Encoding.UTF8, "application/json")
                };
            }
        );

        var options = CreateOptions();
        options.Tags = ["env:production", "app:platform"];
        var provider = new ScalewaySecretManagerConfigurationProvider(options, httpClient);

        // Act
        provider.Load();

        // Assert
        capturedUrl.Should().Contain("tags=env%3Aproduction");
        capturedUrl.Should().Contain("tags=app%3Aplatform");
    }

    [Fact]
    public void Load_ShouldUseCorrectApiPath()
    {
        // Arrange
        string? capturedUrl = null;
        var httpClient = CreateMockHttpClient(request =>
            {
                if (request.RequestUri!.PathAndQuery.Contains("/secrets?"))
                {
                    capturedUrl = request.RequestUri.PathAndQuery;
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"secrets": [], "total_count": 0}""", Encoding.UTF8, "application/json")
                };
            }
        );

        var options = CreateOptions();
        options.Region = "nl-ams";
        options.ProjectId = "my-project";
        var provider = new ScalewaySecretManagerConfigurationProvider(options, httpClient);

        // Act
        provider.Load();

        // Assert
        capturedUrl.Should().Contain("/secret-manager/v1beta1/regions/nl-ams/secrets");
        capturedUrl.Should().Contain("project_id=my-project");
    }

    private static HttpResponseMessage CreateListSecretsResponse((string Id, string Name)[] secrets)
    {
        var secretObjects = secrets.Select(s => new { id = s.Id, name = s.Name, status = "ready" });
        var response = new { secrets = secretObjects, total_count = secrets.Length };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreateAccessSecretResponse(string plainTextValue)
    {
        var base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainTextValue));
        var response = new { data = base64Value, revision = 1, secret_id = "test", type = "opaque" };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
        };
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
