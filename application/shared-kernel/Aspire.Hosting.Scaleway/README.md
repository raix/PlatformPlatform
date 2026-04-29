# Aspire.Hosting.Scaleway

.NET Aspire hosting package for Scaleway cloud services. Provides resource definitions, local dev containers, deployment pipeline, cost estimation, and data protection policies for EU-sovereign cloud infrastructure.

## Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Deployment environment with budget
var scaleway = builder.AddScalewayEnvironment("production", region: ScalewayRegion.FrPar)
    .WithMonthlyBudget(100m);

// Database: Scaleway RDB in production, PostgreSQL container locally
var db = builder.AddScalewayRdbInstance("postgres")
    .RunAsPostgresContainer(c => c
        .WithDataVolume("my-app-postgres-data")
        .WithLifetime(ContainerLifetime.Persistent)
    )
    .PublishAsScalewayRdb(config =>
    {
        config.Engine = "PostgreSQL-16";
        config.NodeType = "DB-DEV-S";
    });

var accountDb = db.AddDatabase("account-database", "account");
var mainDb = db.AddDatabase("main-database", "main");

// Cache: Scaleway Redis in production, Redis container locally
var cache = builder.AddScalewayRedisCluster("cache")
    .RunAsRedisContainer(c => c.WithLifetime(ContainerLifetime.Persistent))
    .PublishAsScalewayRedis(config =>
    {
        config.NodeType = "RED1-MICRO";
        config.Version = "7.0";
    });

// Storage: Scaleway Object Storage in production, SeaweedFS locally
var storage = builder.AddScalewayObjectStorage("storage")
    .RunAsSeaweedFsContainer(s3Port: 8333)
    .PublishAsScalewayObjectStorage();

// Email: Scaleway TEM in production, Mailpit locally
builder.AddScalewayTemDomain("email")
    .RunAsMailpitContainer(httpPort: 9003, smtpPort: 9004);

// Wire resources to projects
builder.AddProject<MyApi>("api")
    .WithReference(accountDb)
    .WithS3Storage(storage)
    .WithScalewayCockpit(ScalewayRegion.FrPar, "my-cockpit-data-source-id")
    .WithCustomDomain("api.example.com")
    .PublishAsScalewayContainer(config =>
    {
        config.MemoryLimitMb = 512;
        config.MinScale = 1;
        config.MaxScale = 10;
    });

await builder.Build().RunAsync();
```

## Features

### Resource Types

38 Scaleway services are supported with auto-generated resource types. The most commonly used:

| Service | Resource | Extension | Local Dev |
|---------|----------|-----------|-----------|
| Managed PostgreSQL | `ScalewayRdbInstanceResource` | `AddScalewayRdbInstance()` | `RunAsPostgresContainer()` |
| Managed Redis | `ScalewayRedisClusterResource` | `AddScalewayRedisCluster()` | `RunAsRedisContainer()` |
| Object Storage (S3) | `ScalewayObjectStorageResource` | `AddScalewayObjectStorage()` | `RunAsSeaweedFsContainer()` / `RunAsMinioContainer()` |
| Transactional Email | `ScalewayTemDomainResource` | `AddScalewayTemDomain()` | `RunAsMailpitContainer()` |
| Container Registry | `ScalewayRegistryNamespaceResource` | `AddScalewayRegistryNamespace()` | — |
| Serverless Containers | `ScalewayServerlessContainerResource` | `AddScalewayServerlessContainer()` | — |
| Secrets Manager | `ScalewaySecretResource` | `AddScalewaySecretSecret()` | — |

Additional generated resources cover Compute, Networking, Kubernetes, Messaging, Security, Observability, and more.

### Local Development Containers

Each `RunAs*Container()` extension creates a Docker container for local development and transparently redirects connection strings. In publish mode, the real Scaleway resource is used instead.

```csharp
// Local dev: starts a PostgreSQL container on a random port
// Production: provisions a Scaleway RDB instance
var db = builder.AddScalewayRdbInstance("postgres")
    .RunAsPostgresContainer(c => c.WithDataVolume("data").WithLifetime(ContainerLifetime.Persistent));

// AddDatabase works in both modes - creates databases on the local container or Scaleway RDB
var appDb = db.AddDatabase("app-db", "myapp");
```

Available local containers:

| Extension | Docker Image | Purpose |
|-----------|-------------|---------|
| `RunAsPostgresContainer()` | `postgres:16` | PostgreSQL database |
| `RunAsRedisContainer()` | `redis:7` | Redis cache |
| `RunAsSeaweedFsContainer()` | `chrislusf/seaweedfs` | S3-compatible storage |
| `RunAsMinioContainer()` | `minio/minio` | S3-compatible storage with web console |
| `RunAsMailpitContainer()` | `axllent/mailpit` | Email capture with web UI |

### Publish Targets

Publish targets configure how resources are deployed to Scaleway. Use callbacks to customize per environment:

```csharp
.PublishAsScalewayRdb(config =>
{
    config.Engine = "PostgreSQL-16";
    config.NodeType = builder.Configuration["Scaleway:Rdb:NodeType"] ?? "DB-DEV-S";
    config.IsHaCluster = builder.Configuration["Scaleway:Rdb:HA"] == "true";
    config.VolumeSizeInGb = 50;
    config.DeletionPolicy = DeletionPolicy.Retain; // Default for databases
})
```

### Deployment Pipeline

The deployment step provisions resources via the Scaleway REST API with tag-based idempotency. No Terraform or external state required.

```csharp
// Full deploy
await ScalewayDeploymentStep.DeployAsync(environment, resources, cancellationToken);

// Dry run - shows what would happen without making changes
var changes = await ScalewayDeploymentStep.DryRunAsync(environment, resources, apiClient, cancellationToken);
foreach (var change in changes)
{
    Console.WriteLine($"  {change.ChangeType}  {change.ResourceName}  {change.Description}");
    if (change.IsBlocked) Console.WriteLine("    ⚠️  BLOCKED");
}
```

The deployment creates shared infrastructure first (private network, container registry), then provisions each resource attached to the private network.

### Data Protection

Resources are classified by risk level. Dangerous changes are blocked automatically:

| Change | Classification | Behavior |
|--------|---------------|----------|
| Region change (database) | **Blocked** | Requires recreation = data loss |
| Engine change (database) | **Blocked** | Requires recreation = data loss |
| Node type change | **Warning** | May cause brief downtime |
| Delete database | **Blocked** | Default `DeletionPolicy.Retain` |
| Delete container | **Safe** | Default `DeletionPolicy.Delete` |
| Scale change | **Safe** | No downtime |

Override deletion policy explicitly when needed:

```csharp
.PublishAsScalewayRdb(config =>
{
    config.DeletionPolicy = DeletionPolicy.Delete; // I know what I'm doing
})
```

### Cost Estimation

The pricing client fetches the Scaleway Product Catalog and estimates monthly costs:

```csharp
using var pricing = new ScalewayPricingClient();

var summary = await pricing.EstimateDeploymentCostAsync(resources, ScalewayRegion.FrPar);
Console.WriteLine($"Estimated monthly cost: €{summary.TotalMonthlyPrice:F2}");
foreach (var estimate in summary.Estimates)
{
    Console.WriteLine($"  {estimate.ResourceType}: €{estimate.MonthlyPrice:F2}/month ({estimate.Details})");
}
```

### Budget Enforcement

Set a maximum monthly budget per environment. Deployments exceeding the budget are blocked:

```csharp
var staging = builder.AddScalewayEnvironment("staging")
    .WithMonthlyBudget(50m); // €50/month max

// DeploymentPlan.CanDeploy returns false if budget exceeded
var plan = new DeploymentPlan(changes, costSummary,
    new BudgetCheckResult(50m, costSummary.TotalMonthlyPrice, "EUR"));

if (!plan.CanDeploy)
{
    if (plan.ExceedsBudget) Console.WriteLine(plan.BudgetCheck!.Message);
    if (plan.HasBlockedChanges) Console.WriteLine("Blocked changes detected.");
}
```

### Observability

Wire OpenTelemetry to Scaleway Cockpit (Grafana):

```csharp
builder.AddProject<MyApi>("api")
    .WithScalewayCockpit(ScalewayRegion.FrPar, "cockpit-data-source-id");
```

This sets `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`, `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT`, and `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` with bearer token authentication via `COCKPIT_TOKEN`.

### Custom Domains

```csharp
builder.AddProject<MyApi>("api")
    .WithCustomDomain("app.example.com")
    .WithCustomDomain("staging.example.com", region: ScalewayRegion.NlAms);
```

### Secrets Management

Secrets are loaded from Scaleway Secret Manager into .NET configuration at startup:

```csharp
// In SharedInfrastructureConfiguration - automatic when running on Scaleway
if (IsRunningInScaleway)
{
    builder.Configuration.AddScalewaySecretManager(reloadInterval: TimeSpan.FromMinutes(1));
}
```

The `ScalewayTokenSigningClient` reads JWT signing keys directly from the Secret Manager API, mirroring the Azure Key Vault pattern.

### Regions and Zones

```csharp
ScalewayRegion.FrPar   // Paris
ScalewayRegion.NlAms   // Amsterdam
ScalewayRegion.PlWaw   // Warsaw

ScalewayZone.FrPar1    // Paris Zone 1
ScalewayZone.NlAms2    // Amsterdam Zone 2
```

## Code Generator

The `Aspire.Hosting.Scaleway.Generator` CLI generates C# resource types from the Scaleway TypeScript SDK:

```bash
dotnet run --project Aspire.Hosting.Scaleway.Generator -- --output Aspire.Hosting.Scaleway/Generated
```

This fetches `types.gen.ts` from `scaleway/scaleway-sdk-js` on GitHub, parses `Create*Request` types, and generates resource classes, extension methods, and enums for all 38 Scaleway services.

## Environment Variables

### Scaleway credentials (production)

| Variable | Purpose |
|----------|---------|
| `SCW_ACCESS_KEY` | Scaleway API access key |
| `SCW_SECRET_KEY` | Scaleway API secret key |
| `SCW_DEFAULT_PROJECT_ID` | Scaleway project ID |
| `SCW_DEFAULT_REGION` | Default region (e.g., `fr-par`) |

### Application secrets (in Scaleway Secret Manager)

| Secret Name | Purpose |
|-------------|---------|
| `authentication-token-signing-key` | JWT signing key (base64) |
| `authentication-token-issuer` | JWT issuer claim |
| `authentication-token-audience` | JWT audience claim |

### Service configuration

| Variable | Purpose |
|----------|---------|
| `S3_ENDPOINT` | Object Storage endpoint |
| `DATABASE_CONNECTION_STRING` | PostgreSQL connection string |
| `SMTP_HOST` | Email SMTP host |
| `SMTP_PORT` | Email SMTP port |
| `SMTP_USERNAME` | Email SMTP username |
| `SMTP_PASSWORD` | Email SMTP password |
| `SENDER_EMAIL_ADDRESS` | From address for emails |
| `COCKPIT_TOKEN` | Scaleway Cockpit push token |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry collector endpoint |
