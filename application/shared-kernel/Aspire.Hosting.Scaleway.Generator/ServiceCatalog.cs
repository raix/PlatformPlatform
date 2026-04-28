namespace Aspire.Hosting.Scaleway.Generator;

/// <summary>
/// Maps Scaleway service names to categories and tracks per-service configuration.
/// </summary>
public static class ServiceCatalog
{
    private static readonly HashSet<string> SkippedServices = ["std", "test"];

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

    /// <summary>
    /// Override the PascalCase service prefix used in generated type names.
    /// Prevents collisions like ScalewayContainerContainerResource.
    /// </summary>
    private static readonly Dictionary<string, string> ServiceNameOverrides = new()
    {
        ["container"] = "ServerlessContainer",
        ["function"] = "ServerlessFunction",
        ["file"] = "FileStorage",
        ["domain"] = "Dns"
    };

    public static bool IsSkipped(string serviceName) => SkippedServices.Contains(serviceName);

    public static string GetCategory(string serviceName) =>
        ServiceToCategory.GetValueOrDefault(serviceName, "Other");

    public static bool IsZonal(string serviceName) => ZonalServices.Contains(serviceName);

    public static string GetServicePrefix(string serviceName) =>
        ServiceNameOverrides.GetValueOrDefault(serviceName, CSharpEmitter.ToPascalCase(serviceName));

    public static IReadOnlyCollection<string> AllServices => ServiceToCategory.Keys;
}
