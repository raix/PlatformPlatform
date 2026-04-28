---
name: upgrade-packages
description: Upgrade all backend (.NET/NuGet) and frontend (npm) dependencies to the latest versions. Drives the developer CLI's update-packages command, fixes the CLI itself when it produces a wrong outcome, and produces clean per-package commits for any upgrade that needs more than a version bump.
---

# Upgrade Packages

Bring all backend and frontend dependencies to their latest versions using the developer CLI's `update-packages` command. Fix the CLI when it produces a wrong outcome. Make clean per-package commits for upgrades that need more than a version change.

The project always runs the latest version of every package. **Don't quit, give up, or recommend reverting just because something is non-trivial** — research, debug, push through. The exceptions below are the only acceptable reasons to skip an upgrade.

## Permanent Exceptions

*No permanent exceptions at this time. Application Insights has been replaced with pure OpenTelemetry.*

## Principles

1. **Use `update-packages` for every bump.** Pass `-e` / `--exclude` to scope a run.
2. **The CLI must produce a correct outcome. If it doesn't, fix the CLI.** The fix lives in `developer-cli/Commands/UpdatePackagesCommand.cs`. Hand-editing `package.json` / `Directory.Packages.props` or running raw `npm` / `dotnet` commands as a workaround is unacceptable — the next person hitting the bug deserves the fix.
3. **Backend first, then frontend.**
4. **Atomic commits.** Trivial bumps go into one bulk commit per side. Anything needing more (code change, config change, API rename, dependency change beyond a version) gets its own commit with the change.
5. **Research majors online.** Read the changelog, release notes, and GitHub issues for any major. If a new major exposes cheap, obvious improvements, adopt them in the same commit. If adoption is non-trivial, ship the upgrade and note the follow-up.
6. **Verify smartly.** Run only what's needed per commit, then a full regression at the end:
   - **Per upgrade**: `build` + `lint` is enough for trivial bumps.
   - **e2e**: only after risky upgrades (majors that touch runtime, build tools, or i18n).
   - **format**: only when code changes, or after upgrading formatter / linter tooling (JetBrains, oxfmt, etc.).
   - **Final regression**: after all upgrades are committed, run the full set — `build`, `format`, `lint`, `test`, `e2e` for both backend and frontend. If it fails, backtrack to the offending commit and fix.
7. **Push through.** When an upgrade misbehaves, your job is to figure out *why*. Reverting is the last resort, only after evidence the version is genuinely unusable.

## Workflow

1. **Verify clean baseline** — `git status` clean, build/lint/test/e2e green. Fix or stop if not.
2. **Dry-run** — `update-packages --dry-run` to see what's outdated; identify likely non-trivial upgrades.
3. **Backend bulk** — `update-packages --backend` excluding the permanent exceptions. Pull anything non-trivial out of the bulk for its own commit afterwards. Verify, commit.
4. **Frontend bulk** — `update-packages --frontend` excluding any package already known to need its own commit. Verify, commit.
5. **Per-package commits** for the rest. For each: research the major, apply the upgrade, make required code/config changes, adopt cheap new features, verify, commit.
6. **Commit any CLI fixes** you made along the way as their own commits, separate from package upgrades.
7. **Final regression** — full `build`, `format`, `lint`, `test`, `e2e` for backend and frontend. If anything fails, backtrack to the offending commit and fix. Then summarise to the user: what moved, what was skipped (with reason), what was adopted, what's deferred.

## Success

Every non-exception package on its latest version. Each non-trivial upgrade in its own clear commit. The CLI is better than when you started — every bug you tripped over is fixed at the source. Build, lint, tests, e2e all green at HEAD.
