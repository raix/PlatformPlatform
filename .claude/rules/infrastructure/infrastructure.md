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

Scaleway injects secrets as environment variables into Serverless Containers. No secrets SDK or Key Vault equivalent needed.

Key environment variables in production:
- `SCW_ACCESS_KEY`, `SCW_SECRET_KEY` — Scaleway API credentials
- `SCW_DEFAULT_PROJECT_ID`, `SCW_DEFAULT_REGION` — Project and region
- `S3_ENDPOINT` — Object Storage endpoint
- `DATABASE_CONNECTION_STRING` — PostgreSQL connection string
- `AUTHENTICATION_TOKEN_SIGNING_KEY`, `AUTHENTICATION_TOKEN_ISSUER`, `AUTHENTICATION_TOKEN_AUDIENCE` — JWT configuration
- `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SENDER_EMAIL_ADDRESS` — Email (TEM)

## Telemetry

Pure OpenTelemetry with OTLP exporter. No Application Insights. Configure `OTEL_EXPORTER_OTLP_ENDPOINT` to point to Scaleway Cockpit (Grafana) or any OTLP-compatible collector.
