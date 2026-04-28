## Build, Test, and Format

Always use MCP tools (`build`, `test`, `format`, `lint`, `run`, `restart`, `stop`, `end_to_end`) instead of running dotnet/npm/npx commands directly. Run `build` first, then remaining tools with `noBuild=true`.

On MCP failures fall back to the CLI (`[CLI_ALIAS] build --quiet`, `[CLI_ALIAS] test --quiet`, etc.).

**Slow:** Aspire restart, backend format, backend lint, end-to-end tests. **Fast:** frontend format/lint, backend test. If any slow operation is needed, run everything in parallel Task agents. End-to-end tests use `waitForAspire=true`.

**Aspire**: The `run`, `restart`, and `stop` MCP tools manage the AppHost at [APP_URL]. Use `restart` when backend changes or hot reload breaks. In the agentic workflow, only the Guardian agent calls these. All other agents must notify the Guardian if they need Aspire restarted.

Never commit, amend, or revert without explicit user instruction each time. Commit messages: one descriptive line in imperative form, no description body.

## Application URL

Whenever you see `[APP_URL]`, replace it with the configured value.

```
APP_URL="https://localhost:9000"
```

## CLI Alias Configuration

Whenever you see `[CLI_ALIAS]`, replace it with the configured value.

```
CLI_ALIAS="pp"
```

## Product Management Tool

Whenever you see `[PRODUCT_MANAGEMENT_TOOL]`, replace it with the configured value.

```
PRODUCT_MANAGEMENT_TOOL="Linear"
```

When working with [features] or [tasks], read `.claude/reference/product-management/[PRODUCT_MANAGEMENT_TOOL].md` to learn how to look them up, how to update status, and how generic statuses like [Active], [Review], [Completed] map to the tool. Read the [feature] for full context and the [task] for specific requirements.

## Auto Memory

Never write to or edit any auto memory files (MEMORY.md or any file in a memory directory). These files are managed by the user only.

## Source of Truth

Always verify paths, names, and API routes against the actual codebase. Never rely on memory, cached context, or prior session knowledge for these. Always look them up. Only read files within the git repository unless explicitly asked to look elsewhere.

## Project Structure

This is a mono repository with multiple self-contained systems (SCS), each being a small monolith. All SCSs follow the same structure.

- [application](/application): Contains application code, one folder per SCS, plus shared-kernel and shared-webapp.
- [cloud-infrastructure](/cloud-infrastructure): Infrastructure as Code for Scaleway deployment.
- [developer-cli](/developer-cli): A .NET CLI tool for automating common developer tasks.
