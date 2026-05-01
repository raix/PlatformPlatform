using System.Text.Json;

namespace Aspire.Hosting.Scaleway.Deployment;

/// <summary>
///     Compares desired resource state (from AppHost) against actual state (from Scaleway API)
///     and produces a list of changes with safety classifications.
///     Blocks dangerous changes (data loss) and flags warnings (potential downtime).
/// </summary>
public sealed class DeploymentPlanner
{
    /// <summary>
    ///     Fields that cannot be changed without recreating the resource (causes data loss).
    ///     Changing these is always blocked.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ImmutableFields = new()
    {
        ["rdb"] = ["region", "engine"],
        ["redis"] = ["zone"],
        ["object-storage"] = ["region"]
    };

    /// <summary>
    ///     Fields that can be changed but may cause downtime.
    ///     These produce warnings but are not blocked.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> WarningFields = new()
    {
        ["rdb"] = ["node_type", "is_ha_cluster"],
        ["redis"] = ["node_type", "cluster_size"]
    };

    /// <summary>
    ///     Default deletion policies per resource type.
    ///     Data-bearing resources default to Retain.
    /// </summary>
    public static DeletionPolicy GetDefaultDeletionPolicy(string resourceType)
    {
        return resourceType switch
        {
            "rdb" => DeletionPolicy.Retain,
            "redis" => DeletionPolicy.Retain,
            "object-storage" => DeletionPolicy.Retain,
            "secret" => DeletionPolicy.Retain,
            _ => DeletionPolicy.Delete
        };
    }

    public DeploymentChange PlanCreate(string resourceName, string resourceType)
    {
        return new DeploymentChange(resourceName, DeploymentChangeType.Create, DeploymentChangeSeverity.Safe, $"Create {resourceType} '{resourceName}'");
    }

    public DeploymentChange PlanNoChange(string resourceName, string resourceType)
    {
        return new DeploymentChange(resourceName, DeploymentChangeType.NoChange, DeploymentChangeSeverity.Safe, $"{resourceType} '{resourceName}' is up to date");
    }

    public DeploymentChange PlanDelete(string resourceName, string resourceType, DeletionPolicy policy)
    {
        if (policy == DeletionPolicy.Retain)
        {
            return new DeploymentChange(resourceName, DeploymentChangeType.NoChange, DeploymentChangeSeverity.Safe, $"{resourceType} '{resourceName}' removed from AppHost but retained (DeletionPolicy.Retain)");
        }

        var defaultPolicy = GetDefaultDeletionPolicy(resourceType);
        if (defaultPolicy == DeletionPolicy.Retain)
        {
            return new DeploymentChange(resourceName, DeploymentChangeType.Delete, DeploymentChangeSeverity.Blocked, $"Cannot delete {resourceType} '{resourceName}': data-bearing resources default to Retain. Set DeletionPolicy.Delete explicitly to override.");
        }

        return new DeploymentChange(resourceName, DeploymentChangeType.Delete, DeploymentChangeSeverity.Safe, $"Delete {resourceType} '{resourceName}'");
    }

    public DeploymentChange[] PlanUpdate(string resourceName, string resourceType, Dictionary<string, (string? Current, string? Desired)> fieldChanges)
    {
        var changes = new List<DeploymentChange>();
        var immutable = ImmutableFields.GetValueOrDefault(resourceType, []);
        var warning = WarningFields.GetValueOrDefault(resourceType, []);

        foreach (var (field, (current, desired)) in fieldChanges)
        {
            if (current == desired)
            {
                continue;
            }

            if (immutable.Contains(field))
            {
                changes.Add(new DeploymentChange(resourceName, DeploymentChangeType.Update, DeploymentChangeSeverity.Blocked, $"Cannot change {field} on '{resourceName}' from '{current}' to '{desired}': this requires recreation and causes data loss. Delete the resource manually first."));
            }
            else if (warning.Contains(field))
            {
                changes.Add(new DeploymentChange(resourceName, DeploymentChangeType.Update, DeploymentChangeSeverity.Warning, $"Changing {field} on '{resourceName}' from '{current}' to '{desired}' may cause brief downtime."));
            }
            else
            {
                changes.Add(new DeploymentChange(resourceName, DeploymentChangeType.Update, DeploymentChangeSeverity.Safe, $"Update {field} on '{resourceName}' from '{current}' to '{desired}'"));
            }
        }

        return changes.ToArray();
    }

    /// <summary>
    ///     Compares a desired RDB config against an existing Scaleway RDB instance and returns planned changes.
    /// </summary>
    public DeploymentChange[] PlanRdbUpdate(string resourceName, ScalewayRdbPublishConfig desired, JsonElement existing)
    {
        var changes = new Dictionary<string, (string? Current, string? Desired)>();

        CompareField(changes, "region", existing, "region", desired.Region.ToApiString());
        CompareField(changes, "engine", existing, "engine", desired.Engine);
        CompareField(changes, "node_type", existing, "node_type", desired.NodeType);
        CompareField(changes, "is_ha_cluster", existing, "is_ha_cluster", desired.IsHaCluster.ToString().ToLowerInvariant());

        return PlanUpdate(resourceName, "rdb", changes);
    }

    /// <summary>
    ///     Compares a desired Redis config against an existing Scaleway Redis cluster and returns planned changes.
    /// </summary>
    public DeploymentChange[] PlanRedisUpdate(string resourceName, ScalewayRedisPublishConfig desired, JsonElement existing)
    {
        var changes = new Dictionary<string, (string? Current, string? Desired)>();

        CompareField(changes, "zone", existing, "zone", desired.Zone.ToApiString());
        CompareField(changes, "node_type", existing, "node_type", desired.NodeType);
        CompareField(changes, "cluster_size", existing, "cluster_size", desired.ClusterSize.ToString());

        return PlanUpdate(resourceName, "redis", changes);
    }

    private static void CompareField(Dictionary<string, (string? Current, string? Desired)> changes, string fieldName, JsonElement existing, string jsonProperty, string desiredValue)
    {
        var currentValue = existing.TryGetProperty(jsonProperty, out var prop)
            ? prop.ValueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => prop.GetRawText(),
                _ => prop.GetString()
            }
            : null;

        changes[fieldName] = (currentValue, desiredValue);
    }
}
