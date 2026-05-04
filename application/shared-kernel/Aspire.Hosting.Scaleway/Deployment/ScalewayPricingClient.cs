using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Fetches pricing from the Scaleway Product Catalog API and estimates monthly costs for resources.
///     The Product Catalog is a public API (no authentication required).
///     Catalog responses are cached on disk in <c>~/.platformplatform/scaleway-pricing-cache-{region}.json</c>
///     with a 24h TTL so repeated dry-runs avoid the network round-trip. The cache can be disabled by
///     setting <c>SCW_PRICING_CACHE_DISABLED=1</c>.
/// </summary>
public sealed class ScalewayPricingClient(HttpClient httpClient, string? cacheDirectory = null, bool? cacheDisabled = null) : IDisposable
{
    private const string DefaultCatalogBaseUrl = "https://api.scaleway.com";
    private const decimal HoursPerMonth = 730m;
    private const decimal SecondsPerHour = 3600m;
    private const decimal ContainerVCpuPricePerSecond = 0.00001m;
    private const decimal ContainerMemoryPricePerGbPerSecond = 0.000001m;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly string _cacheDirectory = cacheDirectory ?? DefaultCacheDirectory();
    private readonly bool _cacheDisabled = cacheDisabled ?? Environment.GetEnvironmentVariable("SCW_PRICING_CACHE_DISABLED") == "1";

    // Process-level cache for the catalog. The disk cache is the cross-process backing store; this
    // layer earns its keep when SCW_PRICING_CACHE_DISABLED=1 (CI / E2E tests) and the disk path is off.
    private readonly ConcurrentDictionary<string, Dictionary<string, CatalogProduct>> _inMemoryCache = new(StringComparer.OrdinalIgnoreCase);

    public ScalewayPricingClient() : this(new HttpClient { BaseAddress = new Uri(ResolveBaseUrl()) })
    {
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    /// <summary>
    ///     Estimates the monthly cost of a resource configuration.
    /// </summary>
    public async Task<CostEstimate> EstimateRdbCostAsync(ScalewayRdbPublishConfig config, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(config.Region, cancellationToken);
        var hourlyRate = FindPrice(catalog, config.NodeType);
        var multiplier = config.IsHaCluster ? 2m : 1m;

        return new CostEstimate(
            "",
            ScalewayResourceTypes.Rdb,
            config.NodeType,
            hourlyRate * HoursPerMonth * multiplier,
            "EUR",
            config.IsHaCluster ? $"{config.NodeType} x2 (HA)" : config.NodeType
        );
    }

    /// <summary>
    ///     Estimates the monthly cost of a Redis cluster.
    /// </summary>
    public async Task<CostEstimate> EstimateRedisCostAsync(ScalewayRedisPublishConfig config, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(config.Zone.ToRegion(), cancellationToken);
        var hourlyRate = FindPrice(catalog, config.NodeType);

        return new CostEstimate(
            "",
            ScalewayResourceTypes.Redis,
            config.NodeType,
            hourlyRate * HoursPerMonth * config.ClusterSize,
            "EUR",
            config.ClusterSize > 1 ? $"{config.NodeType} x{config.ClusterSize}" : config.NodeType
        );
    }

    /// <summary>
    ///     Estimates the monthly cost of a Serverless Container.
    ///     Returns a range since containers scale between min and max.
    /// </summary>
    public Task<CostEstimate> EstimateContainerCostAsync(ScalewayContainerPublishConfig config, CancellationToken cancellationToken = default)
    {
        // Serverless Containers bill per vCPU-second and per GB-second (free tier not modelled here).
        var secondsPerMonth = HoursPerMonth * SecondsPerHour;
        var vCpus = config.CpuLimitMillicores / 1000m;
        var memoryGb = config.MemoryLimitMb / 1024m;
        var costPerInstancePerMonth = (vCpus * ContainerVCpuPricePerSecond + memoryGb * ContainerMemoryPricePerGbPerSecond) * secondsPerMonth;

        var minMonthlyCost = config.MinScale > 0 ? costPerInstancePerMonth * config.MinScale : 0m;
        var maxMonthlyCost = costPerInstancePerMonth * config.MaxScale;

        return Task.FromResult(new CostEstimate(
                "",
                ScalewayResourceTypes.Container,
                $"{config.MemoryLimitMb}MB/{config.CpuLimitMillicores}mVCPU",
                minMonthlyCost,
                "EUR",
                $"€{minMonthlyCost:F2}-€{maxMonthlyCost:F2}/month ({config.MinScale}-{config.MaxScale} scale)"
            )
        );
    }

    /// <summary>
    ///     Estimates costs for all resources in a deployment and returns a summary.
    /// </summary>
    public async Task<DeploymentCostSummary> EstimateDeploymentCostAsync(
        IEnumerable<IResource> resources,
        ScalewayRegion defaultRegion,
        CancellationToken cancellationToken = default)
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
                estimates.Add(estimate with { ResourceName = resource.Name });
            }
        }

        return new DeploymentCostSummary(estimates, estimates.Sum(e => e.MonthlyPrice), "EUR");
    }

    private async Task<Dictionary<string, CatalogProduct>> GetCatalogAsync(ScalewayRegion region, CancellationToken cancellationToken)
    {
        var regionKey = region.ToApiString();

        if (_inMemoryCache.TryGetValue(regionKey, out var cached))
        {
            return cached;
        }

        var fromDisk = _cacheDisabled ? null : TryReadDiskCache(regionKey);
        if (fromDisk is not null)
        {
            _inMemoryCache[regionKey] = fromDisk;
            return fromDisk;
        }

        var fetched = await FetchCatalogAsync(regionKey, cancellationToken);
        _inMemoryCache[regionKey] = fetched;

        if (!_cacheDisabled && fetched.Count > 0)
        {
            TryWriteDiskCache(regionKey, fetched);
        }

        return fetched;
    }

    private async Task<Dictionary<string, CatalogProduct>> FetchCatalogAsync(string regionKey, CancellationToken cancellationToken)
    {
        var url = $"/product-catalog/v2alpha1/public-catalog/products?region={regionKey}&page_size=100";
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Fail-soft: return empty catalog so dry-run still produces a plan with €0 estimates.
            return new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var result = new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);

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

                result[variant] = new CatalogProduct(variant, hourlyPrice);
            }
        }

        return result;
    }

    private Dictionary<string, CatalogProduct>? TryReadDiskCache(string regionKey)
    {
        var path = CacheFilePath(regionKey);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("updated_at", out var updatedAtProp) ||
                !DateTimeOffset.TryParse(updatedAtProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var updatedAt))
            {
                return null;
            }

            if (DateTimeOffset.UtcNow - updatedAt > CacheTtl)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("products", out var products))
            {
                return null;
            }

            var result = new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);
            foreach (var product in products.EnumerateObject())
            {
                if (decimal.TryParse(product.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                {
                    result[product.Name] = new CatalogProduct(product.Name, price);
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt cache or permission problem: pretend it isn't there. The fetch path will refresh.
            return null;
        }
    }

    private void TryWriteDiskCache(string regionKey, Dictionary<string, CatalogProduct> catalog)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);

            var products = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (variant, product) in catalog)
            {
                products[variant] = product.HourlyPrice.ToString(CultureInfo.InvariantCulture);
            }

            var payload = new
            {
                updated_at = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                products
            };

            var path = CacheFilePath(regionKey);
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(payload));
            File.Move(tempPath, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cache writes are best-effort — never fail a deploy because of disk problems.
        }
    }

    private string CacheFilePath(string regionKey)
    {
        return Path.Combine(_cacheDirectory, $"scaleway-pricing-cache-{regionKey}.json");
    }

    private static string ResolveBaseUrl()
    {
        return Environment.GetEnvironmentVariable("SCW_API_URL") ?? DefaultCatalogBaseUrl;
    }

    private static string DefaultCacheDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".platformplatform");
    }

    private static decimal FindPrice(Dictionary<string, CatalogProduct> catalog, string nodeType)
    {
        return catalog.TryGetValue(nodeType, out var product) ? product.HourlyPrice : 0m;
    }
}

public sealed record CostEstimate(
    string ResourceName,
    string ResourceType,
    string NodeType,
    decimal MonthlyPrice,
    string Currency,
    string Details
);

public sealed record DeploymentCostSummary(IReadOnlyList<CostEstimate> Estimates, decimal TotalMonthlyPrice, string Currency);

internal sealed record CatalogProduct(string Variant, decimal HourlyPrice);
