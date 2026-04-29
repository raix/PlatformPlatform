---
paths: cloud-infrastructure/**
description: Rules for cloud infrastructure and deployment using Scaleway
---

# Infrastructure Rules

Guidelines for Scaleway cloud infrastructure, deployment patterns, and the Aspire.Hosting.Scaleway package.

## Cloud Provider

PlatformPlatform deploys to **Scaleway** (EU-sovereign cloud). All Azure dependencies have been removed.

## Aspire.Hosting.Scaleway Package

The `Aspire.Hosting.Scaleway` package in `application/shared-kernel/Aspire.Hosting.Scaleway/` provides:

- **38 Scaleway services** with auto-generated resource types from the Scaleway TypeScript SDK
- **Code generator** (`Aspire.Hosting.Scaleway.Generator/`) that parses `types.gen.ts` from `scaleway-sdk-js`
- **LocalDev containers**: `RunAsPostgresContainer()`, `RunAsRedisContainer()`, `RunAsSeaweedFsContainer()`, `RunAsMailpitContainer()`
- **Publish targets**: `PublishAsScalewayRdb()`, `PublishAsScalewayRedis()`, `PublishAsScalewayContainer()`, `PublishAsScalewayObjectStorage()`

## AppHost Resource Declarations

Resources are declared in `application/AppHost/Program.cs` using Scaleway types:

```csharp
// Database: Scaleway RDB → local PostgreSQL container
var postgres = builder.AddScalewayRdbInstance("postgres")
    .RunAsPostgresContainer(c => c.WithDataVolume(...).WithLifetime(ContainerLifetime.Persistent));
var accountDb = postgres.AddDatabase("account-database", "account");

// Storage: Scaleway Object Storage → local SeaweedFS (S3-compatible)
var storage = builder.AddScalewayObjectStorage("object-storage")
    .RunAsSeaweedFsContainer(s3Port: 8333);

// Email: Scaleway TEM → local Mailpit
builder.AddScalewayTemDomain("mail-server")
    .RunAsMailpitContainer(httpPort: 9003, smtpPort: 9004);
```

## Environment Detection

Use `SharedInfrastructureConfiguration.IsRunningInScaleway` to detect cloud vs local dev. This checks for the `SCW_SECRET_KEY` environment variable.

## Secrets Management

Two layers, mirroring the original Azure Key Vault pattern:

- **Scaleway Secret Manager** — `ScalewayTokenSigningClient` reads JWT signing key, issuer, and audience from the Secret Manager REST API at startup (like `AzureTokenSigningClient` read from Key Vault)
- **Configuration provider** — `ScalewaySecretManagerConfigurationProvider` loads secrets into .NET `IConfiguration` with periodic refresh (like `AddAzureKeyVault`)
- **Local dev** — `DevelopmentTokenSigningClient` reads from .NET user secrets (unchanged)

Key environment variables in production:
- `SCW_ACCESS_KEY`, `SCW_SECRET_KEY` — Scaleway API credentials
- `SCW_DEFAULT_PROJECT_ID`, `SCW_DEFAULT_REGION` — Project and region
- `S3_ENDPOINT` — Object Storage endpoint
- `DATABASE_CONNECTION_STRING` — PostgreSQL connection string
- `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SENDER_EMAIL_ADDRESS` — Email (TEM)

Secrets stored in Scaleway Secret Manager (read via API, not env vars):
- `authentication-token-signing-key` — JWT signing key (base64)
- `authentication-token-issuer` — JWT issuer claim
- `authentication-token-audience` — JWT audience claim

## Telemetry

Pure OpenTelemetry with OTLP exporter. No Application Insights. Scaleway Cockpit (Grafana) receives traces, metrics, and logs.

Wire Cockpit to projects in the AppHost:
```csharp
builder.AddProject<MyApi>("api")
    .WithScalewayCockpit(ScalewayRegion.FrPar, "data-source-id");
```

This sets per-signal OTLP endpoints and bearer token auth via `COCKPIT_TOKEN` env var. Scaleway Cockpit only supports HTTP/protobuf transport.
