using System.Text.Json;

namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Executes the Scaleway deployment pipeline.
///     Iterates all resources with publish annotations, creates shared infrastructure
///     (private network, registry, namespace), then provisions each resource.
///     Mirrors the CDKPublishingStep pattern from the AWS Aspire package.
/// </summary>
public static class ScalewayDeploymentStep
{
    public static async Task DeployAsync(
        ScalewayEnvironmentResource environment,
        IEnumerable<IResource> resources,
        IDeployApprover? approver = null,
        CancellationToken cancellationToken = default)
    {
        using var apiClient = new ScalewayApiClient(environment.CredentialConfig);
        await DeployAsync(environment, resources, apiClient, approver, cancellationToken);
    }

    internal static async Task DeployAsync(
        ScalewayEnvironmentResource environment,
        IEnumerable<IResource> resources,
        ScalewayApiClient apiClient,
        IDeployApprover? approver = null,
        CancellationToken cancellationToken = default)
    {
        approver ??= new AutoApprover();
        var region = environment.CredentialConfig.DefaultRegion.ToApiString();
        var projectId = environment.CredentialConfig.DefaultProjectId!;

        // Shared infrastructure is auto-applied (no approver gate). The registry namespace's id
        // isn't consumed downstream — containers live in the container namespace, not this one.
        var privateNetwork = await ProvisionPrivateNetworkAsync(apiClient, region, projectId, environment.DefaultsProvider.PrivateNetwork, cancellationToken);
        await ProvisionRegistryNamespaceAsync(apiClient, region, projectId, environment.DefaultsProvider.Registry, cancellationToken);

        foreach (var resource in resources)
        {
            var publishAnnotation = resource.Annotations.OfType<IScalewayPublishTargetAnnotation>().FirstOrDefault();
            if (publishAnnotation is null)
            {
                continue;
            }

            var resourceType = publishAnnotation switch
            {
                PublishAsScalewayRdbAnnotation => ScalewayResourceTypes.Rdb,
                PublishAsScalewayRedisAnnotation => ScalewayResourceTypes.Redis,
                PublishAsScalewayContainerAnnotation => ScalewayResourceTypes.Container,
                _ => publishAnnotation.GetType().Name
            };

            var decision = await approver.ApproveAsync(resource.Name, resourceType, cancellationToken);
            if (decision == DeployApproverDecision.Abort)
            {
                throw new DistributedApplicationException($"Deploy aborted by approver at '{resource.Name}'.");
            }

            if (decision == DeployApproverDecision.Skip)
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
                    await ProvisionContainerAsync(apiClient, region, projectId, resource.Name, containerAnnotation.Config, privateNetwork, cancellationToken);
                    break;
            }
        }
    }

    private static async Task<string> ProvisionPrivateNetworkAsync(
        ScalewayApiClient apiClient,
        string region,
        string projectId,
        ScalewayPrivateNetworkConfig config,
        CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"vpc/v2/regions/{region}/private-networks", region, projectId, config.Name, cancellationToken);
        if (existing is not null)
        {
            return existing.Value.GetProperty("id").GetString()!;
        }

        var result = await apiClient.CreateResourceAsync($"vpc/v2/regions/{region}/private-networks", region,
            new { project_id = projectId, name = config.Name, tags = new[] { "aspire-managed" } }, cancellationToken
        );
        return result.GetProperty("id").GetString()!;
    }

    private static async Task<string> ProvisionRegistryNamespaceAsync(
        ScalewayApiClient apiClient,
        string region,
        string projectId,
        ScalewayRegistryConfig config,
        CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"registry/v1/regions/{region}/namespaces", region, projectId, config.Name, cancellationToken);
        if (existing is not null)
        {
            return existing.Value.GetProperty("id").GetString()!;
        }

        var result = await apiClient.CreateResourceAsync($"registry/v1/regions/{region}/namespaces", region,
            new { project_id = projectId, name = config.Name }, cancellationToken
        );
        return result.GetProperty("id").GetString()!;
    }

    private static async Task ProvisionRdbAsync(
        ScalewayApiClient apiClient,
        string region,
        string projectId,
        string resourceName,
        ScalewayRdbPublishConfig config,
        string privateNetworkId,
        CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"rdb/v1/regions/{region}/instances", region, projectId, resourceName, cancellationToken);
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
                password = ScalewayProvisioner.GeneratePassword(),
                is_ha_cluster = config.IsHaCluster,
                disable_backup = config.DisableBackup,
                volume_size = config.VolumeSizeInGb * 1_000_000_000,
                init_endpoints = new[] { new { private_network = new { private_network_id = privateNetworkId } } },
                tags = new[] { "aspire-managed" }
            }, cancellationToken
        );
    }

    private static async Task ProvisionRedisAsync(
        ScalewayApiClient apiClient,
        string zone,
        string projectId,
        string resourceName,
        ScalewayRedisPublishConfig config,
        string privateNetworkId,
        CancellationToken cancellationToken)
    {
        var existing = await FindByNameAsync(apiClient, $"redis/v1/zones/{zone}/clusters", zone, projectId, resourceName, cancellationToken);
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
                password = ScalewayProvisioner.GeneratePassword(),
                endpoints = new[] { new { private_network = new { id = privateNetworkId } } },
                tags = new[] { "aspire-managed" }
            }, cancellationToken
        );
    }

    private static async Task ProvisionContainerAsync(
        ScalewayApiClient apiClient,
        string region,
        string projectId,
        string resourceName,
        ScalewayContainerPublishConfig config,
        string privateNetworkId,
        CancellationToken cancellationToken)
    {
        var namespaceId = await FindOrCreateContainerNamespaceAsync(apiClient, region, projectId, config, cancellationToken);

        var existing = await FindByNameAsync(apiClient, $"containers/v1beta1/regions/{region}/containers", region, projectId, resourceName, cancellationToken);
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
            }, cancellationToken
        );
    }

    private static async Task<string> FindOrCreateContainerNamespaceAsync(
        ScalewayApiClient apiClient,
        string region,
        string projectId,
        ScalewayContainerPublishConfig config,
        CancellationToken cancellationToken)
    {
        var namespaceName = config.RegistryNamespace ?? "default";
        var existing = await FindByNameAsync(apiClient, $"containers/v1beta1/regions/{region}/namespaces", region, projectId, namespaceName, cancellationToken);
        if (existing is not null)
        {
            return existing.Value.GetProperty("id").GetString()!;
        }

        var result = await apiClient.CreateResourceAsync($"containers/v1beta1/regions/{region}/namespaces", region,
            new { project_id = projectId, name = namespaceName }, cancellationToken
        );
        return result.GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement?> FindByNameAsync(
        ScalewayApiClient apiClient,
        string apiPath,
        string regionOrZone,
        string projectId,
        string name,
        CancellationToken cancellationToken)
    {
        var resources = await apiClient.ListResourcesAsync(apiPath, regionOrZone,
            new Dictionary<string, string> { ["project_id"] = projectId, ["name"] = name }, cancellationToken
        );

        var match = resources.FirstOrDefault(r => r.TryGetProperty("name", out var n) && n.GetString() == name);
        return match.ValueKind == JsonValueKind.Undefined ? null : match;
    }

    /// <summary>
    ///     Performs a dry run: compares desired state against actual Scaleway resources and returns a plan
    ///     without making any changes. Uses the DeploymentPlanner for safety classification.
    /// </summary>
    internal static async Task<DeploymentChange[]> DryRunAsync(
        ScalewayEnvironmentResource environment,
        IEnumerable<IResource> resources,
        ScalewayApiClient apiClient,
        CancellationToken cancellationToken = default)
    {
        var region = environment.CredentialConfig.DefaultRegion.ToApiString();
        var projectId = environment.CredentialConfig.DefaultProjectId!;
        var planner = new DeploymentPlanner();

        // Fan out all lookups concurrently — they're independent reads against different endpoints.
        var networkLookup = FindByNameAsync(apiClient, $"vpc/v2/regions/{region}/private-networks", region, projectId, environment.DefaultsProvider.PrivateNetwork.Name, cancellationToken);
        var registryLookup = FindByNameAsync(apiClient, $"registry/v1/regions/{region}/namespaces", region, projectId, environment.DefaultsProvider.Registry.Name, cancellationToken);

        var resourceLookups = new List<(IResource Resource, IScalewayPublishTargetAnnotation Annotation, Task<JsonElement?> Lookup)>();
        foreach (var resource in resources)
        {
            var annotation = resource.Annotations.OfType<IScalewayPublishTargetAnnotation>().FirstOrDefault();
            if (annotation is null) continue;

            var lookup = annotation switch
            {
                PublishAsScalewayRdbAnnotation => FindByNameAsync(apiClient, $"rdb/v1/regions/{region}/instances", region, projectId, resource.Name, cancellationToken),
                PublishAsScalewayRedisAnnotation redisAnnotation => FindByNameAsync(apiClient, $"redis/v1/zones/{redisAnnotation.Config.Zone.ToApiString()}/clusters", redisAnnotation.Config.Zone.ToApiString(), projectId, resource.Name, cancellationToken),
                PublishAsScalewayContainerAnnotation => FindByNameAsync(apiClient, $"containers/v1beta1/regions/{region}/containers", region, projectId, resource.Name, cancellationToken),
                _ => Task.FromResult<JsonElement?>(null)
            };

            resourceLookups.Add((resource, annotation, lookup));
        }

        await Task.WhenAll([networkLookup, registryLookup, .. resourceLookups.Select(l => l.Lookup)]);

        var changes = new List<DeploymentChange>
        {
            networkLookup.Result is not null
                ? planner.PlanNoChange(environment.DefaultsProvider.PrivateNetwork.Name, ScalewayResourceTypes.PrivateNetwork)
                : planner.PlanCreate(environment.DefaultsProvider.PrivateNetwork.Name, ScalewayResourceTypes.PrivateNetwork),
            registryLookup.Result is not null
                ? planner.PlanNoChange(environment.DefaultsProvider.Registry.Name, ScalewayResourceTypes.Registry)
                : planner.PlanCreate(environment.DefaultsProvider.Registry.Name, ScalewayResourceTypes.Registry)
        };

        foreach (var (resource, annotation, lookup) in resourceLookups)
        {
            var existing = lookup.Result;
            switch (annotation)
            {
                case PublishAsScalewayRdbAnnotation rdbAnnotation:
                    if (existing is null)
                    {
                        changes.Add(planner.PlanCreate(resource.Name, ScalewayResourceTypes.Rdb));
                    }
                    else
                    {
                        var updateChanges = planner.PlanRdbUpdate(resource.Name, rdbAnnotation.Config, existing.Value);
                        changes.AddRange(updateChanges.Length > 0 ? updateChanges : [planner.PlanNoChange(resource.Name, ScalewayResourceTypes.Rdb)]);
                    }

                    break;

                case PublishAsScalewayRedisAnnotation redisAnnotation:
                    if (existing is null)
                    {
                        changes.Add(planner.PlanCreate(resource.Name, ScalewayResourceTypes.Redis));
                    }
                    else
                    {
                        var updateChanges = planner.PlanRedisUpdate(resource.Name, redisAnnotation.Config, existing.Value);
                        changes.AddRange(updateChanges.Length > 0 ? updateChanges : [planner.PlanNoChange(resource.Name, ScalewayResourceTypes.Redis)]);
                    }

                    break;

                case PublishAsScalewayContainerAnnotation:
                    changes.Add(existing is null ? planner.PlanCreate(resource.Name, ScalewayResourceTypes.Container) : planner.PlanNoChange(resource.Name, ScalewayResourceTypes.Container));
                    break;
            }
        }

        return changes.ToArray();
    }
}
