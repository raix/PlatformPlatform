using System.Text.Json;

namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Executes the Scaleway deployment pipeline.
///     Iterates all resources with publish annotations, creates shared infrastructure
///     (private network, registry, namespace), then provisions each resource.
///     Mirrors the CDKPublishingStep pattern from the AWS Aspire package.
/// </summary>
internal static class ScalewayDeploymentStep
{
    /// <summary>
    ///     Tag stamped on every resource provisioned by this pipeline. Lets a future
    ///     reaper distinguish managed resources from anything created by hand.
    /// </summary>
    private const string AspireManagedTag = "aspire-managed";

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
                    await ProvisionContainerAsync(apiClient, region, projectId, resource, containerAnnotation.Config, privateNetwork, environment.CredentialConfig, cancellationToken);
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
            new { project_id = projectId, name = config.Name, tags = new[] { AspireManagedTag } }, cancellationToken
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

        var secretClient = new ScalewaySecretClient(apiClient);
        var resourceTags = new[] { AspireManagedTag, $"aspire-resource={resourceName}" };

        // Get-or-create the password BEFORE the RDB create. On a partial-failure retry, the next
        // run finds the orphaned secret and reuses its value — RDB and secret converge to the same
        // password. The RDB existence check above ensures we don't redo any of this on idempotent
        // reruns where the RDB already exists from a prior successful deploy.
        var password = await secretClient.GetOrCreateAsync(
            projectId, region,
            $"rdb-{resourceName}-password",
            ScalewayProvisioner.GeneratePassword,
            resourceTags,
            cancellationToken
        );

        var created = await apiClient.CreateResourceAsync($"rdb/v1/regions/{region}/instances", region, new
            {
                project_id = projectId,
                name = resourceName,
                engine = config.Engine,
                node_type = config.NodeType,
                user_name = config.UserName,
                password,
                is_ha_cluster = config.IsHaCluster,
                disable_backup = config.DisableBackup,
                volume_size = config.VolumeSizeInGb * 1_000_000_000,
                init_endpoints = new[] { new { private_network = new { private_network_id = privateNetworkId } } },
                tags = new[] { AspireManagedTag }
            }, cancellationToken
        );

        await StoreRdbConnectionDetailsAsync(secretClient, projectId, region, resourceName, created, config, resourceTags, cancellationToken);
    }

    /// <summary>
    ///     Persists the RDB endpoint + username so workloads can assemble a connection string from
    ///     Secret Manager without an extra API call. Endpoint shape varies by Scaleway RDB version
    ///     and connectivity mode (private network vs public); we extract defensively.
    /// </summary>
    private static async Task StoreRdbConnectionDetailsAsync(
        ScalewaySecretClient secretClient,
        string projectId,
        string region,
        string resourceName,
        JsonElement instance,
        ScalewayRdbPublishConfig config,
        string[] tags,
        CancellationToken cancellationToken)
    {
        if (instance.TryGetProperty("endpoint", out var endpoint))
        {
            await WriteEndpointSecretsAsync(endpoint);
        }
        else if (instance.TryGetProperty("endpoints", out var endpoints) && endpoints.ValueKind == JsonValueKind.Array)
        {
            var first = endpoints.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                await WriteEndpointSecretsAsync(first);
            }
        }

        await secretClient.SetAsync(projectId, region, $"rdb-{resourceName}-username", config.UserName, tags, cancellationToken);

        return;

        async Task WriteEndpointSecretsAsync(JsonElement ep)
        {
            var host = ep.TryGetProperty("hostname", out var h) ? h.GetString() : null;
            host ??= ep.TryGetProperty("ip", out var ip) ? ip.GetString() : null;
            var port = ep.TryGetProperty("port", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 5432;

            if (host is not null)
            {
                await secretClient.SetAsync(projectId, region, $"rdb-{resourceName}-host", host, tags, cancellationToken);
                await secretClient.SetAsync(projectId, region, $"rdb-{resourceName}-port", port.ToString(), tags, cancellationToken);
            }
        }
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
                tags = new[] { AspireManagedTag }
            }, cancellationToken
        );
    }

    private static async Task ProvisionContainerAsync(
        ScalewayApiClient apiClient,
        string region,
        string projectId,
        IResource resource,
        ScalewayContainerPublishConfig config,
        string privateNetworkId,
        ScalewayCredentialConfig credentialConfig,
        CancellationToken cancellationToken)
    {
        var resourceName = resource.Name;
        var namespaceId = await FindOrCreateContainerNamespaceAsync(apiClient, region, projectId, config, cancellationToken);

        var existing = await FindByNameAsync(apiClient, $"containers/v1beta1/regions/{region}/containers", region, projectId, resourceName, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var environmentVariables = await ResolveEnvironmentVariablesAsync(resource, credentialConfig, cancellationToken);

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
                environment_variables = environmentVariables,
                tags = new[] { AspireManagedTag }
            }, cancellationToken
        );
    }

    /// <summary>
    ///     Resolves the env vars Aspire wants on this workload via its <c>EnvironmentCallbackAnnotation</c>s,
    ///     and unconditionally adds the <c>SCW_*</c> credentials so the workload can authenticate to
    ///     Scaleway Secret Manager (where database credentials and other secrets live).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         v1 uses the deploy-job's broad-scope SCW key for every workload. Per-workload least-privilege
    ///         keys land in a follow-up commit on the same task — this commit closes the bigger gap that
    ///         no env vars at all reached deployed containers.
    ///     </para>
    ///     <para>
    ///         Only literal string values are injected. <see cref="IValueProvider" /> values
    ///         (e.g. <c>ParameterResource</c>, <c>WithReference(database)</c> connection strings) are skipped:
    ///         resolving them would block on parameter-store / endpoint discovery that doesn't exist in publish mode,
    ///         and workloads read the values they actually need (database connection string, OAuth secrets) from
    ///         Scaleway Secret Manager at runtime instead of from env vars.
    ///     </para>
    /// </remarks>
    private static async Task<Dictionary<string, string>> ResolveEnvironmentVariablesAsync(
        IResource resource,
        ScalewayCredentialConfig credentialConfig,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SCW_ACCESS_KEY"] = credentialConfig.AccessKey ?? string.Empty,
            ["SCW_SECRET_KEY"] = credentialConfig.SecretKey ?? string.Empty,
            ["SCW_DEFAULT_PROJECT_ID"] = credentialConfig.DefaultProjectId ?? string.Empty,
            ["SCW_DEFAULT_REGION"] = credentialConfig.DefaultRegion.ToApiString()
        };

        var callbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray();
        if (callbacks.Length == 0) return resolved;

        var raw = new Dictionary<string, object>(StringComparer.Ordinal);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish);
        var context = new EnvironmentCallbackContext(executionContext, resource, raw, cancellationToken);

        foreach (var callback in callbacks)
        {
            await callback.Callback(context);
        }

        foreach (var (key, value) in raw)
        {
            if (value is string literal)
            {
                resolved[key] = literal;
            }
        }

        return resolved;
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
