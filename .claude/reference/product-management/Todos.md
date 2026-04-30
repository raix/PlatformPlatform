# Todos

Todos is a file-based product management tool that does NOT use MCP. [Features] and [tasks] are stored as markdown files in a top-level `todos/` folder. A single board file (`todos/kanban.md`) groups tasks by status and links to per-task files in `todos/tasks/`.

## Terminology Mapping

| Generic Term | Todos |
|---|---|
| `[Feature]` | A `## Feature: <name>` section in `kanban.md` (groups related tasks) |
| `[Task]` | A markdown file in `todos/tasks/<id>-<slug>.md` |
| `[Subtask]` | Bullet point inside the task file's body |

## Status Mapping

**For [Feature]:**
| Generic Status | Todos |
|---|---|
| `[Planned]` | All tasks in feature are `[Planned]` |
| `[Active]` | At least one task is `[Active]` or `[Review]` |
| `[Resolved]` | All tasks are `[Completed]` |

**For [Task]:**
| Generic Status | Todos |
|---|---|
| `[Ideas]` | `[Ideas]` (not yet committed work) |
| `[Planned]` | `[Planned]` |
| `[Active]` | `[Active]` |
| `[Review]` | `[Review]` |
| `[Completed]` | `[Completed]` |

`[Ideas]` is for forward-looking concepts that may become real tasks. They have a title and a paragraph of context, but no acceptance criteria or scope yet. Promote an idea by changing its status to `[Planned]` and adding scope.

## ID Mapping

| Generic ID | Todos |
|---|---|
| `featureId` | Feature name (slug, e.g. `aspire-hosting-scaleway`) |
| `taskId` | Numeric task ID (e.g. `39`) — matches the filename prefix |

## File Structure

```
todos/
├── kanban.md                       # the board: tasks grouped by status, by feature
└── tasks/
    ├── 39-wire-aspire-deploy.md
    ├── 40-...md
    └── ...
```

## Initialization

If `todos/` does not exist, create it:

```
todos/
  kanban.md   # see template below
  tasks/      # empty, with .gitkeep
```

`todos/` is gitignored — it's a local-only task tracker, not committed alongside the code. This keeps personal task lists out of the repo (especially important for a template repo that others fork).

## kanban.md Format

The board is a single markdown file with one heading per status, and feature sub-sections under each status. Each task is one bullet that links to its task file.

```markdown
# Kanban

## Ideas

### Feature: future-hosting-platform

- [#1 Single pp deploy command](tasks/1-single-pp-deploy-command.md)

## Active

### Feature: aspire-hosting-scaleway

- [#39 Wire aspire deploy to Scaleway](tasks/39-wire-aspire-deploy.md)

## Review

_(empty)_

## Planned

### Feature: aspire-hosting-scaleway

- [#40 Add Scaleway Cockpit metrics export](tasks/40-cockpit-metrics.md)

## Completed

### Feature: aspire-hosting-scaleway

- [#38 Add monthly budget enforcement](tasks/38-monthly-budget.md)
```

## Task File Format

Each task lives in its own markdown file under `todos/tasks/`. The filename is `<id>-<kebab-slug>.md`. The frontmatter holds metadata; the body holds context, acceptance criteria, and subtasks.

```markdown
---
id: 39
title: Wire aspire deploy to Scaleway
feature: aspire-hosting-scaleway
status: Active
---

# #39 Wire aspire deploy to Scaleway

**Purpose:** Hook `ScalewayDeploymentStep.DeployAsync` into Aspire's deploy
lifecycle so `aspire deploy` triggers dry-run + plan + apply against Scaleway.

**Out of scope:** Cockpit metrics export, custom domains.

**Acceptance criteria:**
- `aspire deploy` against the AppHost provisions all `PublishAsScaleway*` resources.
- Dry-run is shown to the user; deploy is blocked when the plan has blocked changes or exceeds budget.
- Tests cover the happy path and the budget-exceeded path.

## Subtasks

- Add `AddScalewayEnvironment` to AppHost
- Annotate resources with `PublishAsScaleway*`
- Wire `ScalewayDeploymentStep` into a `IDistributedApplicationLifecycleHook`
- Surface dry-run output to stdout
```

## Critical Rules

- `featureId` is a feature name slug.
- `taskId` is the numeric ID matching the filename prefix (e.g. `"39"` for `tasks/39-...md`).
- Update status by editing the task file's `status:` frontmatter **and** moving its bullet under the matching `## <Status>` heading in `kanban.md`.
- Task IDs are sequential and never reused. To find the next ID, take the highest number in `todos/tasks/` and add one.
- Don't delete completed tasks — they stay under `## Completed` for history.
- Don't put task content in `kanban.md`. The board only links to task files.

## Reading and Updating

- **Look up a task:** open `todos/tasks/<id>-*.md` directly, or find it via `kanban.md`.
- **Update status:** edit the `status:` frontmatter in the task file, then move its bullet in `kanban.md`.
- **Create a task:** add a new file under `todos/tasks/`, then add a bullet in the matching section of `kanban.md`.
- **List tasks in a feature:** grep `feature: <name>` in `todos/tasks/`, or look at the feature's sub-sections in `kanban.md`.
