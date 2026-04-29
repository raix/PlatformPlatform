using System.Text.Json;

namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Fetches pricing from the Scaleway Product Catalog API and estimates monthly costs for resources.
/// The Product Catalog is a public API (no authentication required).
/// </summary>
public sealed class ScalewayPricingClient : IDisposable
{
    private const string CatalogBaseUrl = "https://api.scaleway.com";
    private readonly HttpClient _httpClient;
    private Dictionary<string, CatalogProduct>? _catalogCache;

    public ScalewayPricingClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(CatalogBaseUrl) };
    }

    /// <summary>
    /// Estimates the monthly cost of a resource configuration.
    /// </summary>
    public async Task<CostEstimate> EstimateRdbCostAsync(ScalewayRdbPublishConfig config, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(config.Region, cancellationToken);
        var hourlyRate = FindPrice(catalog, config.NodeType);
        var monthlyHours = 730m; // Average hours per month
        var multiplier = config.IsHaCluster ? 2m : 1m;

        return new CostEstimate(
            ResourceType: "rdb",
            NodeType: config.NodeType,
            MonthlyPrice: hourlyRate * monthlyHours * multiplier,
            Currency: "EUR",
            Details: config.IsHaCluster ? $"{config.NodeType} x2 (HA)" : config.NodeType
        );
    }

    /// <summary>
    /// Estimates the monthly cost of a Redis cluster.
    /// </summary>
    public async Task<CostEstimate> EstimateRedisCostAsync(ScalewayRedisPublishConfig config, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(config.Zone.ToRegion(), cancellationToken);
        var hourlyRate = FindPrice(catalog, config.NodeType);
        var monthlyHours = 730m;

        return new CostEstimate(
            ResourceType: "redis",
            NodeType: config.NodeType,
            MonthlyPrice: hourlyRate * monthlyHours * config.ClusterSize,
            Currency: "EUR",
            Details: config.ClusterSize > 1 ? $"{config.NodeType} x{config.ClusterSize}" : config.NodeType
        );
    }

    /// <summary>
    /// Estimates the monthly cost of a Serverless Container.
    /// Returns a range since containers scale between min and max.
    /// </summary>
    public Task<CostEstimate> EstimateContainerCostAsync(ScalewayContainerPublishConfig config, CancellationToken cancellationToken = default)
    {
        // Serverless Containers: €0.00001/vCPU-s + €0.000001/GB-s
        // 200k vCPU-s + 400k GB-s free per month
        var vCpuPerSecond = 0.00001m;
        var memoryPerGbPerSecond = 0.000001m;
        var secondsPerMonth = 730m * 3600m;

        var vCpus = config.CpuLimitMillicores / 1000m;
        var memoryGb = config.MemoryLimitMb / 1024m;

        var minMonthlyCost = config.MinScale > 0
            ? (vCpus * vCpuPerSecond + memoryGb * memoryPerGbPerSecond) * secondsPerMonth * config.MinScale
            : 0m;

        var maxMonthlyCost = (vCpus * vCpuPerSecond + memoryGb * memoryPerGbPerSecond) * secondsPerMonth * config.MaxScale;

        return Task.FromResult(new CostEstimate(
            ResourceType: "container",
            NodeType: $"{config.MemoryLimitMb}MB/{config.CpuLimitMillicores}mVCPU",
            MonthlyPrice: minMonthlyCost,
            Currency: "EUR",
            Details: $"€{minMonthlyCost:F2}-€{maxMonthlyCost:F2}/month ({config.MinScale}-{config.MaxScale} scale)"
        ));
    }

    /// <summary>
    /// Estimates costs for all resources in a deployment and returns a summary.
    /// </summary>
    public async Task<DeploymentCostSummary> EstimateDeploymentCostAsync(
        IEnumerable<IResource> resources, ScalewayRegion defaultRegion, CancellationToken cancellationToken = default)
    {
        var estimates = new List<CostEstimate>();

        foreach (var resource in resources)
        {
            var annotation = resource.Annotations.OfType<IScalewayPublishTargetAnnotation>().FirstOrDefault();
            if (annotation is null) continue;

            var estimate = annotation switch
            {
                PublishAsScalewayRdbAnnotation rdb => await EstimateRdbCostAsync(rdb.Config, cancellationToken),
                PublishAsScalewayRedisAnnotation redis => await EstimateRedisCostAsync(redis.Config, cancellationToken),
                PublishAsScalewayContainerAnnotation container => await EstimateContainerCostAsync(container.Config, cancellationToken),
                _ => null
            };

            if (estimate is not null)
            {
                estimates.Add(estimate with { ResourceType = resource.Name });
            }
        }

        return new DeploymentCostSummary(estimates, estimates.Sum(e => e.MonthlyPrice), "EUR");
    }

    private async Task<Dictionary<string, CatalogProduct>> GetCatalogAsync(ScalewayRegion region, CancellationToken cancellationToken)
    {
        if (_catalogCache is not null) return _catalogCache;

        var url = $"/product-catalog/v2alpha1/public-catalog/products?region={region.ToApiString()}&page_size=100";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _catalogCache = new Dictionary<string, CatalogProduct>();
            return _catalogCache;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        _catalogCache = new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty("products", out var products))
        {
            foreach (var product in products.EnumerateArray())
            {
                var variant = product.TryGetProperty("variant", out var v) ? v.GetString() : null;
                if (variant is null) continue;

                var hourlyPrice = 0m;
                if (product.TryGetProperty("price", out var price) &&
                    price.TryGetProperty("retail_price", out var retailPrice))
                {
                    var units = retailPrice.TryGetProperty("units", out var u) ? u.GetInt64() : 0;
                    var nanos = retailPrice.TryGetProperty("nanos", out var n) ? n.GetInt64() : 0;
                    hourlyPrice = units + nanos / 1_000_000_000m;
                }

                _catalogCache[variant] = new CatalogProduct(variant, hourlyPrice);
            }
        }

        return _catalogCache;
    }

    private static decimal FindPrice(Dictionary<string, CatalogProduct> catalog, string nodeType)
    {
        return catalog.TryGetValue(nodeType, out var product) ? product.HourlyPrice : 0m;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public sealed record CostEstimate(string ResourceType, string NodeType, decimal MonthlyPrice, string Currency, string Details);

public sealed record DeploymentCostSummary(IReadOnlyList<CostEstimate> Estimates, decimal TotalMonthlyPrice, string Currency);

internal sealed record CatalogProduct(string Variant, decimal HourlyPrice);
