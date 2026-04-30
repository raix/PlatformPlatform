using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayPricingTests
{
    [Fact]
    public async Task EstimateRdbCost_ShouldCalculateFromCatalog()
    {
        // Arrange
        var httpClient = CreateMockCatalogClient([
                CreateCatalogProduct("DB-DEV-S", 0.012m)
            ]
        );
        using var pricing = new ScalewayPricingClient(httpClient);
        var config = new ScalewayRdbPublishConfig { NodeType = "DB-DEV-S" };

        // Act
        var estimate = await pricing.EstimateRdbCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().Be(0.012m * 730m);
        estimate.Currency.Should().Be("EUR");
        estimate.NodeType.Should().Be("DB-DEV-S");
    }

    [Fact]
    public async Task EstimateRdbCost_WithHighAvailability_ShouldDoublePrice()
    {
        // Arrange
        var httpClient = CreateMockCatalogClient([
                CreateCatalogProduct("DB-GP-S", 0.10m)
            ]
        );
        using var pricing = new ScalewayPricingClient(httpClient);
        var config = new ScalewayRdbPublishConfig { NodeType = "DB-GP-S", IsHaCluster = true };

        // Act
        var estimate = await pricing.EstimateRdbCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().Be(0.10m * 730m * 2m);
        estimate.Details.Should().Contain("HA");
    }

    [Fact]
    public async Task EstimateRedisCost_ShouldMultiplyByClusterSize()
    {
        // Arrange
        var httpClient = CreateMockCatalogClient([
                CreateCatalogProduct("RED1-MICRO", 0.008m)
            ]
        );
        using var pricing = new ScalewayPricingClient(httpClient);
        var config = new ScalewayRedisPublishConfig { NodeType = "RED1-MICRO", ClusterSize = 3 };

        // Act
        var estimate = await pricing.EstimateRedisCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().Be(0.008m * 730m * 3m);
        estimate.Details.Should().Contain("x3");
    }

    [Fact]
    public async Task EstimateContainerCost_WhenMinScaleZero_ShouldReturnZeroMinimum()
    {
        // Arrange
        using var pricing = new ScalewayPricingClient(CreateMockCatalogClient([]));
        var config = new ScalewayContainerPublishConfig { MinScale = 0, MaxScale = 10, MemoryLimitMb = 256, CpuLimitMillicores = 140 };

        // Act
        var estimate = await pricing.EstimateContainerCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().Be(0m);
        estimate.Details.Should().Contain("0-10 scale");
    }

    [Fact]
    public async Task EstimateContainerCost_WhenMinScalePositive_ShouldCalculateBaseCost()
    {
        // Arrange
        using var pricing = new ScalewayPricingClient(CreateMockCatalogClient([]));
        var config = new ScalewayContainerPublishConfig { MinScale = 2, MaxScale = 10, MemoryLimitMb = 1024, CpuLimitMillicores = 1000 };

        // Act
        var estimate = await pricing.EstimateContainerCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().BeGreaterThan(0m);
        estimate.Details.Should().Contain("2-10 scale");
    }

    [Fact]
    public async Task EstimateDeploymentCost_ShouldSumAllResources()
    {
        // Arrange
        var httpClient = CreateMockCatalogClient([
                CreateCatalogProduct("DB-DEV-S", 0.012m),
                CreateCatalogProduct("RED1-MICRO", 0.008m)
            ]
        );
        using var pricing = new ScalewayPricingClient(httpClient);

        var rdb = new ScalewayRdbInstanceResource("account-db");
        rdb.Annotations.Add(new PublishAsScalewayRdbAnnotation { Config = new ScalewayRdbPublishConfig { NodeType = "DB-DEV-S" } });

        var redis = new ScalewayRedisClusterResource("session-cache");
        redis.Annotations.Add(new PublishAsScalewayRedisAnnotation { Config = new ScalewayRedisPublishConfig { NodeType = "RED1-MICRO" } });

        // Act
        var summary = await pricing.EstimateDeploymentCostAsync([rdb, redis], ScalewayRegion.FrPar);

        // Assert
        summary.Estimates.Should().HaveCount(2);
        summary.TotalMonthlyPrice.Should().Be((0.012m + 0.008m) * 730m);
        summary.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task EstimateDeploymentCost_ShouldSkipResourcesWithoutAnnotation()
    {
        // Arrange
        var httpClient = CreateMockCatalogClient([]);
        using var pricing = new ScalewayPricingClient(httpClient);

        var rdb = new ScalewayRdbInstanceResource("no-annotation");

        // Act
        var summary = await pricing.EstimateDeploymentCostAsync([rdb], ScalewayRegion.FrPar);

        // Assert
        summary.Estimates.Should().BeEmpty();
        summary.TotalMonthlyPrice.Should().Be(0m);
    }

    [Fact]
    public async Task EstimateRdbCost_WhenNodeTypeNotInCatalog_ShouldReturnZero()
    {
        // Arrange
        var httpClient = CreateMockCatalogClient([]);
        using var pricing = new ScalewayPricingClient(httpClient);
        var config = new ScalewayRdbPublishConfig { NodeType = "UNKNOWN-TYPE" };

        // Act
        var estimate = await pricing.EstimateRdbCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().Be(0m);
    }

    [Fact]
    public async Task EstimateRdbCost_WhenCatalogApiFails_ShouldReturnZero()
    {
        // Arrange
        var httpClient = new HttpClient(new FailingHandler()) { BaseAddress = new Uri("https://api.scaleway.com") };
        using var pricing = new ScalewayPricingClient(httpClient);
        var config = new ScalewayRdbPublishConfig { NodeType = "DB-DEV-S" };

        // Act
        var estimate = await pricing.EstimateRdbCostAsync(config);

        // Assert
        estimate.MonthlyPrice.Should().Be(0m);
    }

    [Fact]
    public async Task CatalogIsCached_SecondCallDoesNotFetch()
    {
        // Arrange
        var fetchCount = 0;
        var httpClient = new HttpClient(new CountingHandler(() =>
                {
                    fetchCount++;
                    return CreateCatalogResponse([CreateCatalogProduct("DB-DEV-S", 0.012m)]);
                }
            )
        ) { BaseAddress = new Uri("https://api.scaleway.com") };
        using var pricing = new ScalewayPricingClient(httpClient);
        var config = new ScalewayRdbPublishConfig { NodeType = "DB-DEV-S" };

        // Act
        await pricing.EstimateRdbCostAsync(config);
        await pricing.EstimateRdbCostAsync(config);

        // Assert
        fetchCount.Should().Be(1);
    }

    private static HttpClient CreateMockCatalogClient(JsonElement[] products)
    {
        var handler = new MockHandler(products);
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.scaleway.com") };
    }

    private static JsonElement CreateCatalogProduct(string variant, decimal hourlyPrice)
    {
        var units = (long)Math.Floor(hourlyPrice);
        var nanos = (long)((hourlyPrice - units) * 1_000_000_000m);
        var json = JsonSerializer.Serialize(new
            {
                variant,
                product = "test",
                sku = variant,
                price = new { retail_price = new { units, nanos, currency_code = "EUR" } },
                unit_of_measure = new { unit = "hour", size = 1 }
            }
        );
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static HttpResponseMessage CreateCatalogResponse(JsonElement[] products)
    {
        var json = JsonSerializer.Serialize(new { products, total_count = products.Length });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class MockHandler(JsonElement[] products) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateCatalogResponse(products));
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class CountingHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory());
        }
    }
}
