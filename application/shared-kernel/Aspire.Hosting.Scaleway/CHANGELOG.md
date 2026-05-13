# Changelog

All notable changes to the `Aspire.Hosting.Scaleway` package are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Pre-1.0 versions
may include breaking changes in minor releases; each is called out under `Changed` with a
`BREAKING:` prefix.

## [Unreleased]

### Added

- `.github/workflows/_migrate-database.yml` — reusable workflow that runs EF Core migrations against
  a live Scaleway RDB instance. Two-stage (plan + apply, apply gated by `inputs.apply_migrations`
  + GitHub Environment protection). Reads RDB credentials from Scaleway Secret Manager (see #26)
  and manages a temporary RDB ACL rule for the runner IP. Workers keep their `if (!IsRunningInCloud)`
  skip-guard — this workflow is the only path that mutates schema in cloud, same contract as the
  original Azure setup.
- `.github/workflows/_deploy.yml` — reusable workflow that runs `aspire deploy` against a Scaleway
  environment. Installs the Aspire CLI, logs in to Scaleway Container Registry, sets
  `APPHOST_ENVIRONMENT` so `EnvironmentProfile.Resolve()` picks the right per-env values.
- `.github/workflows/deploy.yml` — top-level deploy orchestrator. On push to `main`: generates a
  version, fans out per-SCS migrations in parallel, runs `aspire deploy` to staging, then to
  production (production gated by GitHub Environment protection — required reviewers).

## [0.1.0-preview] - 2026-05-08

### Added

- Code-generated resource types for 33 Scaleway products (RDB, Redis, Object Storage, Serverless
  Containers, Container Registry, TEM, Secret Manager, Private Networks, Cockpit, …) under
  `Generated/`.
- `AddScalewayEnvironment` to declare a deploy target with credentials, region, and shared infra
  (private network, registry, container namespace).
- `PublishAs*` extensions (`PublishAsScalewayRdb`, `PublishAsScalewayRedis`,
  `PublishAsScalewayObjectStorage`, `PublishAsScalewayContainer`) for declaring publish targets on
  Aspire resources.
- `WithCustomDomain` for binding a custom DNS name + Scaleway-managed TLS to a published container.
- `WithScalewayCockpit` for wiring OTLP export to Scaleway Cockpit.
- `WithMonthlyBudget` for capping monthly cost; deploy aborts above the cap.
- Deployment pipeline integration: `aspire deploy` runs a dry-run, prints a deployment plan
  (changes + cost estimate + budget verdict), and provisions only when nothing is blocked.
- Deployment planner classifies each change as `Safe` / `Warning` / `Blocked` based on per-resource
  immutable-field maps.
- Cost estimation via Scaleway Product Catalog API with on-disk cache at `~/.platformplatform/`
  (24h TTL, can be disabled via `SCW_PRICING_CACHE_DISABLED=1`).
- Step-by-step interactive approver gated by `SCW_DEPLOY_INTERACTIVE=1`.
- Scaleway Secret Manager configuration provider with periodic refresh.
- `ScalewayTokenSigningClient` reads JWT signing keys from Scaleway Secret Manager.
- E2E test harness with in-process mock Scaleway API server.
- RDB credential lifecycle: deploy persists the generated password to Scaleway Secret Manager as
  `rdb-{name}-password`, plus host/port/username post-create. Workloads assemble the connection
  string from these via the existing `AddScalewaySecretManager` provider.
- Container env-var injection: `ScalewayDeploymentStep.ProvisionContainerAsync` resolves Aspire
  `EnvironmentCallbackAnnotation`s and emits literal-string values onto each container, plus
  unconditional `SCW_*` credentials so workloads can authenticate to Secret Manager.
- Scaleway Serverless Container health checks: `/internal-api/live` with 30s interval, threshold
  3. Sent in the container create body so rolling deploys gate traffic on liveness.
- `ScalewaySecretClient` get-or-create wrapper with write-then-create idempotency for the
  partial-failure recovery path.

### Notes

- Suppresses `ASPIREPIPELINES001` because the Aspire Pipelines API is in preview.
- Depends on `Aspire.Hosting` 13.2.4+ and `Aspire.Hosting.Pipelines`.
- `IsRunningInScaleway` was renamed to `IsRunningInCloud` in `SharedKernel.Configuration` to keep
  the public API vendor-neutral while the underlying check still keys off `SCW_SECRET_KEY`.
- `ScalewayObjectStorageResource` is wired via `WithS3Storage(objectStorage)` in AppHost — the
  ergonomics differ from `WithReference` because Aspire's stock connection-string-redirect
  mechanism doesn't compose with generic `AddContainer()` (used for SeaweedFS / MinIO local-dev).
  Future task may close that asymmetry.
