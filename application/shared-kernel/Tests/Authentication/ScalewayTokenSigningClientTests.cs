using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SharedKernel.Authentication.TokenSigning;
using Xunit;

namespace SharedKernel.Tests.Authentication;

public sealed class ScalewayTokenSigningClientTests
{
    [Fact]
    public void Constructor_WhenSecretsExist_ShouldLoadSigningKeyIssuerAndAudience()
    {
        // Arrange
        var signingKey = Convert.ToBase64String(new byte[64]); // 512-bit key
        var httpClient = CreateMockHttpClient(request =>
        {
            var url = request.RequestUri!.PathAndQuery;
            if (url.Contains("secret_name=authentication-token-signing-key"))
            {
                return CreateSecretResponse(signingKey);
            }
            if (url.Contains("secret_name=authentication-token-issuer"))
            {
                return CreateSecretResponse("https://my-app.scaleway.com");
            }
            if (url.Contains("secret_name=authentication-token-audience"))
            {
                return CreateSecretResponse("my-app-audience");
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Act
        var client = new ScalewayTokenSigningClient(httpClient, "fr-par", "test-project");

        // Assert
        client.Issuer.Should().Be("https://my-app.scaleway.com");
        client.Audience.Should().Be("my-app-audience");
    }

    [Fact]
    public void GetSigningCredentials_ShouldReturnHmacSha512Credentials()
    {
        // Arrange
        var signingKey = Convert.ToBase64String(new byte[64]);
        var httpClient = CreateMockHttpClient(_ => CreateSecretResponse(signingKey));
        var client = new ScalewayTokenSigningClient(httpClient, "fr-par", "test-project");

        // Act
        var credentials = client.GetSigningCredentials();

        // Assert
        credentials.Algorithm.Should().Be("HS512");
    }

    [Fact]
    public void GetTokenValidationParameters_ShouldReturnCorrectParameters()
    {
        // Arrange
        var signingKey = Convert.ToBase64String(new byte[64]);
        var httpClient = CreateMockHttpClient(request =>
        {
            var url = request.RequestUri!.PathAndQuery;
            if (url.Contains("secret_name=authentication-token-issuer"))
            {
                return CreateSecretResponse("test-issuer");
            }
            if (url.Contains("secret_name=authentication-token-audience"))
            {
                return CreateSecretResponse("test-audience");
            }
            return CreateSecretResponse(signingKey);
        });
        var client = new ScalewayTokenSigningClient(httpClient, "fr-par", "test-project");

        // Act
        var parameters = client.GetTokenValidationParameters(TimeSpan.FromSeconds(5), true);

        // Assert
        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuer.Should().Be("test-issuer");
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudience.Should().Be("test-audience");
        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        parameters.ValidateLifetime.Should().BeTrue();
        parameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_ShouldCallCorrectApiEndpoints()
    {
        // Arrange
        var capturedUrls = new List<string>();
        var signingKey = Convert.ToBase64String(new byte[64]);
        var httpClient = CreateMockHttpClient(request =>
        {
            capturedUrls.Add(request.RequestUri!.PathAndQuery);
            return CreateSecretResponse(signingKey);
        });

        // Act
        _ = new ScalewayTokenSigningClient(httpClient, "nl-ams", "my-project-id");

        // Assert
        capturedUrls.Should().HaveCount(3);
        capturedUrls.Should().AllSatisfy(url =>
        {
            url.Should().Contain("/secret-manager/v1beta1/regions/nl-ams/secrets-by-path/versions/latest_enabled/access");
            url.Should().Contain("project_id=my-project-id");
        });
    }

    [Fact]
    public void Constructor_ShouldSendAuthTokenHeader()
    {
        // Arrange
        string? capturedAuthToken = null;
        var signingKey = Convert.ToBase64String(new byte[64]);
        var httpClient = CreateMockHttpClient(request =>
        {
            capturedAuthToken ??= request.Headers.TryGetValues("X-Auth-Token", out var values) ? values.First() : null;
            return CreateSecretResponse(signingKey);
        });
        httpClient.DefaultRequestHeaders.Add("X-Auth-Token", "my-secret-key");

        // Act
        _ = new ScalewayTokenSigningClient(httpClient, "fr-par", "test-project");

        // Assert
        capturedAuthToken.Should().Be("my-secret-key");
    }

    private static HttpClient CreateMockHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var messageHandler = new MockHttpMessageHandler(handler);
        return new HttpClient(messageHandler) { BaseAddress = new Uri("https://api.scaleway.com") };
    }

    private static HttpResponseMessage CreateSecretResponse(string plainTextValue)
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
