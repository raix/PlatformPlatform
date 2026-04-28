namespace Aspire.Hosting.Scaleway.Generator;

/// <summary>
/// Maps Scaleway service names to categories and tracks per-service configuration.
/// </summary>
public static class ServiceCatalog
{
    // Skip non-real services and services with hand-written resources
    private static readonly HashSet<string> SkippedServices = ["std", "test", "rdb", "redis", "tem", "registry", "container"];

    private static readonly Dictionary<string, string> ServiceToCategory = new()
    {
        // Compute
        ["instance"] = "Compute",
        ["applesilicon"] = "Compute",
        ["baremetal"] = "Compute",
        ["inference"] = "Compute",
        ["dedibox"] = "Compute",
        // Serverless
        ["container"] = "Serverless",
        ["function"] = "Serverless",
        ["jobs"] = "Serverless",
        ["serverless_sqldb"] = "Serverless",
        // Databases
        ["rdb"] = "Databases",
        ["redis"] = "Databases",
        ["mongodb"] = "Databases",
        ["documentdb"] = "Databases",
        ["searchdb"] = "Databases",
        // Storage
        ["block"] = "Storage",
        ["file"] = "Storage",
        // Networking
        ["vpc"] = "Networking",
        ["vpcgw"] = "Networking",
        ["lb"] = "Networking",
        ["domain"] = "Networking",
        ["flexibleip"] = "Networking",
        ["ipam"] = "Networking",
        ["interlink"] = "Networking",
        ["s2s_vpn"] = "Networking",
        ["edge_services"] = "Networking",
        // Kubernetes
        ["k8s"] = "Kubernetes",
        ["autoscaling"] = "Kubernetes",
        // Containers
        ["registry"] = "Containers",
        // Messaging
        ["mnq"] = "Messaging",
        ["kafka"] = "Messaging",
        // Security
        ["secret"] = "Security",
        ["key_manager"] = "Security",
        ["iam"] = "Security",
        // Observability
        ["cockpit"] = "Observability",
        // Communication
        ["tem"] = "Communication",
        ["mailbox"] = "Communication",
        // Other
        ["account"] = "Other",
        ["billing"] = "Other",
        ["marketplace"] = "Other",
        ["webhosting"] = "Other",
        ["iot"] = "Other",
        ["datalab"] = "Other",
        ["datawarehouse"] = "Other",
        ["qaas"] = "Other",
        ["audit_trail"] = "Other",
        ["environmental_footprint"] = "Other",
        ["product_catalog"] = "Other",
        ["partner"] = "Other"
    };

    /// <summary>
    /// Services that use zones instead of regions.
    /// </summary>
    private static readonly HashSet<string> ZonalServices =
    [
        "instance", "baremetal", "applesilicon", "block", "flexibleip"
    ];

    public static bool IsSkipped(string serviceName) => SkippedServices.Contains(serviceName);

    public static string GetCategory(string serviceName) =>
        ServiceToCategory.GetValueOrDefault(serviceName, "Other");

    public static bool IsZonal(string serviceName) => ZonalServices.Contains(serviceName);

    public static IReadOnlyCollection<string> AllServices => ServiceToCategory.Keys;
}
