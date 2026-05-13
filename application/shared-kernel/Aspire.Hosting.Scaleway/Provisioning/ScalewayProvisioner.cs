using System.Text.Json;

namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Provisions Scaleway cloud resources by calling the Scaleway REST API directly.
///     Uses tag-based find-or-create for idempotent deployments (no external state files).
/// </summary>
public sealed class ScalewayProvisioner : IDisposable
{
    private const string AspireAppTag = "aspire-app";
    private const string AspireResourceTag = "aspire-resource";
    private readonly string _appName;

    private readonly ScalewayApiClient _client;
    private readonly string _projectId;

    public ScalewayProvisioner(ScalewayCredentialConfig credentials, string appName)
    {
        _client = new ScalewayApiClient(credentials);
        _appName = appName;
        _projectId = credentials.DefaultProjectId
                     ?? throw new InvalidOperationException("SCW_DEFAULT_PROJECT_ID is required for provisioning.");
    }

    internal ScalewayProvisioner(HttpClient httpClient, string appName, string projectId)
    {
        _client = new ScalewayApiClient(httpClient);
        _appName = appName;
        _projectId = projectId;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    ///     Provisions or updates a Scaleway RDB instance.
    /// </summary>
    public async Task<ProvisioningResult> ProvisionRdbInstanceAsync(string resourceName, ScalewayRdbPublishConfig config, CancellationToken cancellationToken = default)
    {
        var region = config.Region.ToApiString();
        var tags = BuildTags(resourceName, config.Tags);

        var existing = await FindExistingResourceAsync("rdb/v1/regions/{region}/instances", region, resourceName, cancellationToken);

        if (existing is not null)
        {
            var id = existing.Value.GetProperty("id").GetString()!;
            return new ProvisioningResult(id, existing.Value);
        }

        var body = new
        {
            project_id = _projectId,
            name = resourceName,
            engine = config.Engine,
            node_type = config.NodeType,
            user_name = config.UserName,
            password = GeneratePassword(),
            is_ha_cluster = config.IsHaCluster,
            disable_backup = config.DisableBackup,
            volume_size = config.VolumeSizeInGb * 1_000_000_000,
            tags
        };

        var result = await _client.CreateResourceAsync("rdb/v1/regions/{region}/instances", region, body, cancellationToken);
        return new ProvisioningResult(result.GetProperty("id").GetString()!, result);
    }

    /// <summary>
    ///     Provisions or updates a Scaleway Redis cluster.
    /// </summary>
    public async Task<ProvisioningResult> ProvisionRedisClusterAsync(string resourceName, ScalewayRedisPublishConfig config, CancellationToken cancellationToken = default)
    {
        var zone = config.Zone.ToApiString();
        var tags = BuildTags(resourceName, config.Tags);

        var existing = await FindExistingResourceAsync("redis/v1/zones/{region}/clusters", zone, resourceName, cancellationToken);

        if (existing is not null)
        {
            var id = existing.Value.GetProperty("id").GetString()!;
            return new ProvisioningResult(id, existing.Value);
        }

        var body = new
        {
            project_id = _projectId,
            name = resourceName,
            version = config.Version,
            node_type = config.NodeType,
            cluster_size = config.ClusterSize,
            tls_enabled = config.TlsEnabled,
            user_name = "default",
            password = GeneratePassword(),
            tags
        };

        var result = await _client.CreateResourceAsync("redis/v1/zones/{region}/clusters", zone, body, cancellationToken);
        return new ProvisioningResult(result.GetProperty("id").GetString()!, result);
    }

    /// <summary>
    ///     Finds an existing resource by matching aspire-app and aspire-resource tags.
    /// </summary>
    private async Task<JsonElement?> FindExistingResourceAsync(string apiPath, string regionOrZone, string resourceName, CancellationToken cancellationToken)
    {
        var resources = await _client.ListResourcesAsync(
            apiPath, regionOrZone,
            new Dictionary<string, string>
            {
                ["project_id"] = _projectId,
                ["tags"] = $"{AspireAppTag}={_appName},{AspireResourceTag}={resourceName}"
            },
            cancellationToken
        );

        return resources.Length > 0 ? resources[0] : null;
    }

    private string[] BuildTags(string resourceName, string[]? additionalTags)
    {
        var tags = new List<string>
        {
            $"{AspireAppTag}={_appName}",
            $"{AspireResourceTag}={resourceName}"
        };

        if (additionalTags is not null)
        {
            tags.AddRange(additionalTags);
        }

        return tags.ToArray();
    }

    internal static string GeneratePassword()
    {
        return $"Aspire-{Guid.NewGuid():N}"[..32];
    }
}

/// <summary>
///     Result of a provisioning operation.
/// </summary>
public sealed record ProvisioningResult(string ResourceId, JsonElement ApiResponse);
