---
paths: application/shared-kernel/**/*.csproj,application/shared-kernel/**/CHANGELOG.md
description: Maintain CHANGELOG.md on shippable library packages and update it in the same PR as the change
---

# Package CHANGELOG.md

Guidelines for maintaining changelogs on shippable library packages.

## Implementation

1. Identify shippable packages — separate `.csproj`, public API surface, consumer-facing README. Application code (`account`, `main`, `back-office` SCSs, AppHost, AppGateway) does not need a changelog; release notes for the application as a whole are tracked elsewhere.

2. Maintain the current list of shippable libraries:
   - `application/shared-kernel/Aspire.Hosting.Scaleway`
   - `application/shared-kernel/Scaleway.Sdk` (when present)

   Adding a new shippable package requires creating its `CHANGELOG.md` in the same PR.

3. Use the [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) format. Sections in this order, omit those with no entries in a given release:
   - `Added` — new features / API.
   - `Changed` — behaviour changes. **Breaking changes go here, prefixed `BREAKING:`** so consumers can grep for them.
   - `Deprecated` — features marked for removal in a future version.
   - `Removed` — features removed in this version.
   - `Fixed` — bug fixes.
   - `Security` — security-relevant changes.

4. Update the changelog in the same PR as the change. Add the entry under the `[Unreleased]` heading at the top.

5. An entry is required when the change is consumer-visible:
   - Added / removed / renamed types, extension methods, configuration shapes.
   - Added / removed generated resources.
   - Behaviour changes visible to AppHost or workload code.

6. An entry is **not** required for:
   - Refactors that don't change the public API (private renames, internal restructuring).
   - Test-only changes.
   - README or doc-only changes (unless documenting a behaviour change).
   - Build / CI / tooling changes that don't affect consumers.

   When in doubt: if a consumer's code or build could behave differently after this PR, add an entry.

7. Pre-1.0 packages (currently both Scaleway packages) follow [SemVer](https://semver.org/spec/v2.0.0.html) with the explicit caveat that minor versions may break compatibility. Breaking changes still go under `Changed` with a `BREAKING:` prefix.

8. When cutting a release:
   - Move all `[Unreleased]` entries under a new `[X.Y.Z] - YYYY-MM-DD` heading.
   - Bump the package version in the `.csproj`.
   - Leave `[Unreleased]` empty at the top.
   - Tag the commit `<package>-vX.Y.Z`.

## Examples

### Example 1 — Adding a new extension method

```markdown
## [Unreleased]

### Added

- `WithMonthlyBudget(decimal maxMonthlyEur)` extension on `IResourceBuilder<ScalewayEnvironmentResource>` —
  deployments that exceed the configured budget are blocked during dry-run. Override via the
  `SCW_MONTHLY_BUDGET` environment variable.
```

### Example 2 — Breaking change

```markdown
## [Unreleased]

### Changed

- BREAKING: `WithS3Storage(objectStorage)` removed. Use `WithReference(objectStorage)` instead;
  `RunAsSeaweedFsContainer` / `RunAsMinioContainer` now attach `ConnectionStringRedirectAnnotation`
  so the standard reference mechanism resolves to the local container's S3 endpoint in run mode
  and the Scaleway cloud endpoint in publish mode.
```
