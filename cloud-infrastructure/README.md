## Cloud Infrastructure

This folder will contain infrastructure configuration for Scaleway deployment.

The application uses the `Aspire.Hosting.Scaleway` package (in `application/shared-kernel/Aspire.Hosting.Scaleway/`) to define and provision cloud resources. The deployment model uses direct Scaleway API calls with tag-based idempotency.

### Current deployment model

Resources are declared in the AppHost (`application/AppHost/Program.cs`) and provisioned via the `ScalewayDeploymentStep` which calls the Scaleway REST API directly. No external IaC tool (Terraform, Bicep) is required.

### Scaleway services used

- **Serverless Containers** — Application hosting (APIs, workers)
- **Managed PostgreSQL (RDB)** — Database per self-contained system
- **Object Storage** — S3-compatible blob storage (avatars, logos)
- **Transactional Email (TEM)** — SMTP email delivery
- **Container Registry** — Docker image storage
- **Secret Manager** — JWT keys, SMTP credentials
- **Cockpit** — Grafana-based observability (via OpenTelemetry)
- **Private Networks** — Network isolation between resources
