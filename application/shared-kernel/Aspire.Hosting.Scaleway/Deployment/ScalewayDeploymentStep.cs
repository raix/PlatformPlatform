using System.Text.Json;

namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Executes the Scaleway deployment pipeline.
/// Iterates all resources with publish annotations, creates shared infrastructure
/// (private network, registry, namespace), then provisions each resource.
/// Mirrors the CDKPublishingStep pattern from the AWS Aspire package.
/// </summary>
public static class ScalewayDeploymentStep
{
    public static async Task DeployAsync(
        ScalewayEnvironmentResource environment,
        IEnumerable<IResource> resources,
        CancellationToken cancellationToken)
    {
        var credentials = environment.CredentialConfig;
        using var apiClient = new ScalewayApiClient(credentials);
        var region = credentials.DefaultRegion.ToApiString();
        var projectId = credentials.DefaultProjectId!;

        // Step 1: Create shared infrastructure
        var privateNetwork = await ProvisionPrivateNetworkAsync(apiClient, region, projectId, environment.DefaultsProvider.PrivateNetwork, cancellationToken);
        var registryNamespace = await ProvisionRegistryNamespaceAsync(apiClient, region, projectId, environment.DefaultsProvider.Registry, cancellationToken);

        // Step 2: Provision each resource with a publish annotation
        foreach (var resource in resources)
        {
            var publishAnnotation = resource.Annotations.OfType<IScalewayPublishTargetAnnotation>().FirstOrDefault();
            if (publishAnnotation is null)
            {
                continue;
            }

            switch (publishAnnotation)
            {
                case PublishAsScalewayRdbAnnotation rdbAnnotation:
                    await ProvisionRdbAsync(apiClient, region, projectId, resource.Name, rdbAnnotation.Config, privateNetwork, cancellationToken);
                    break;
                case PublishAsScalewayRedisAnnotation redisAnnotation:
                    var zone = redisAnnotation.Config.Zone.ToApiString();
                    await ProvisionRedisAsync(apiClient, zone, projectId, resource.Name, redisAnnotation.Config, privateNetwork, cancellationToken);
                    break;
                case PublishAsScalewayContainerAnnotation containerAnnotation:
                    await ProvisionContainerAsync(apiClient, region, projectId, resource.Name, containerAnnotation.Config, registryNamespace, privateNetwork, cancellationToken);
                    break;
            }
        }
    }

    private static async Task<string> ProvisionPrivateNetworkAsync(
        ScalewayApiClient apiClient, string region, string projectId,
        ScalewayPrivateNetworkConfig config, CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"vpc/v2/regions/{region}/private-networks", region, projectId, config.Name, "private_networks", cancellationToken);
        if (existing is not null)
        {
            return existing.Value.GetProperty("id").GetString()!;
        }

        var result = await apiClient.CreateResourceAsync($"vpc/v2/regions/{region}/private-networks", region,
            new { project_id = projectId, name = config.Name, tags = new[] { "aspire-managed" } }, cancellationToken);
        return result.GetProperty("id").GetString()!;
    }

    private static async Task<string> ProvisionRegistryNamespaceAsync(
        ScalewayApiClient apiClient, string region, string projectId,
        ScalewayRegistryConfig config, CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"registry/v1/regions/{region}/namespaces", region, projectId, config.Name, "namespaces", cancellationToken);
        if (existing is not null)
        {
            return existing.Value.GetProperty("id").GetString()!;
        }

        var result = await apiClient.CreateResourceAsync($"registry/v1/regions/{region}/namespaces", region,
            new { project_id = projectId, name = config.Name }, cancellationToken);
        return result.GetProperty("id").GetString()!;
    }

    private static async Task ProvisionRdbAsync(
        ScalewayApiClient apiClient, string region, string projectId, string resourceName,
        ScalewayRdbPublishConfig config, string privateNetworkId, CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"rdb/v1/regions/{region}/instances", region, projectId, resourceName, "instances", cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await apiClient.CreateResourceAsync($"rdb/v1/regions/{region}/instances", region, new
        {
            project_id = projectId,
            name = resourceName,
            engine = config.Engine,
            node_type = config.NodeType,
            user_name = config.UserName,
            password = GeneratePassword(),
            is_ha_cluster = config.IsHaCluster,
            disable_backup = config.DisableBackup,
            volume_size = config.VolumeSizeInGb * 1_000_000_000,
            init_endpoints = new[] { new { private_network = new { private_network_id = privateNetworkId } } },
            tags = new[] { "aspire-managed" }
        }, cancellationToken);
    }

    private static async Task ProvisionRedisAsync(
        ScalewayApiClient apiClient, string zone, string projectId, string resourceName,
        ScalewayRedisPublishConfig config, string privateNetworkId, CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"redis/v1/zones/{zone}/clusters", zone, projectId, resourceName, "clusters", cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await apiClient.CreateResourceAsync($"redis/v1/zones/{zone}/clusters", zone, new
        {
            project_id = projectId,
            name = resourceName,
            version = config.Version,
            node_type = config.NodeType,
            cluster_size = config.ClusterSize,
            tls_enabled = config.TlsEnabled,
            user_name = "default",
            password = GeneratePassword(),
            endpoints = new[] { new { private_network = new { id = privateNetworkId } } },
            tags = new[] { "aspire-managed" }
        }, cancellationToken);
    }

    private static async Task ProvisionContainerAsync(
        ScalewayApiClient apiClient, string region, string projectId, string resourceName,
        ScalewayContainerPublishConfig config, string registryNamespaceId, string privateNetworkId,
        CancellationToken cancellationToken)
    {
        // Find or create the container namespace
        var namespaceId = await FindOrCreateContainerNamespaceAsync(apiClient, region, projectId, config, cancellationToken);

        var existing = await FindByNameAsync(apiClient, $"containers/v1beta1/regions/{region}/containers", region, projectId, resourceName, "containers", cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await apiClient.CreateResourceAsync($"containers/v1beta1/regions/{region}/containers", region, new
        {
            namespace_id = namespaceId,
            name = resourceName,
            memory_limit = config.MemoryLimitMb * 1_000_000,
            min_scale = config.MinScale,
            max_scale = config.MaxScale,
            max_concurrency = config.MaxConcurrency,
            timeout = $"{config.TimeoutSeconds}s",
            privacy = config.Privacy,
            port = config.Port,
            private_network_id = privateNetworkId,
            tags = new[] { "aspire-managed" }
        }, cancellationToken);
    }

    private static async Task<string> FindOrCreateContainerNamespaceAsync(
        ScalewayApiClient apiClient, string region, string projectId,
        ScalewayContainerPublishConfig config, CancellationToken cancellationToken)
    {
        var namespaceName = config.RegistryNamespace ?? "default";
        var existing = await FindByNameAsync(apiClient, $"containers/v1beta1/regions/{region}/namespaces", region, projectId, namespaceName, "namespaces", cancellationToken);
        if (existing is not null)
        {
            return existing.Value.GetProperty("id").GetString()!;
        }

        var result = await apiClient.CreateResourceAsync($"containers/v1beta1/regions/{region}/namespaces", region,
            new { project_id = projectId, name = namespaceName }, cancellationToken);
        return result.GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement?> FindByNameAsync(
        ScalewayApiClient apiClient, string apiPath, string regionOrZone, string projectId,
        string name, string arrayProperty, CancellationToken cancellationToken)
    {
        var resources = await apiClient.ListResourcesAsync(apiPath, regionOrZone,
            new Dictionary<string, string> { ["project_id"] = projectId, ["name"] = name }, cancellationToken);

        return resources.FirstOrDefault(r => r.TryGetProperty("name", out var n) && n.GetString() == name);
    }

    private static string GeneratePassword()
    {
        return $"Aspire-{Guid.NewGuid():N}"[..32];
    }
}
