# GitHub Actions — NuGet publish pipelines

Four independent workflows, one per package:

| Workflow | Package | Tag prefix |
|---|---|---|
| `publish-core.yml` | `DuckRun.Core` | `core-v*` |
| `publish-efcore.yml` | `DuckRun.EfCore` | `efcore-v*` |
| `publish-redis.yml` | `DuckRun.Redis` | `redis-v*` |
| `publish-framework.yml` | `DuckRun.Framework` | `framework-v*` |

Each package counts versions on its own via git tags.

## What triggers what

| Branch | Trigger | Destination | Version scheme |
|---|---|---|---|
| `master` | `workflow_dispatch` (manual button) | **nuget.org** | stable — auto-increments patch from the latest `<prefix>-v*` tag, or use the workflow's `version` input to set it explicitly. First publish defaults to `1.0.0`. |
| `test` | push to `test` with relevant paths | **GitHub Packages** | `<base>-test.<run_number>` (prerelease) — `<base>` is the next patch from the latest stable tag, `<run_number>` is GitHub's workflow run counter. |
| `dev` (and everything else) | — | — | Nothing fires. |

The manual master path also creates a git tag (`core-v1.0.3`, etc.) so the next run knows where to count from.

## Required setup

### One repository secret

- **`NUGET_API_KEY`** — a [nuget.org API key](https://www.nuget.org/account/apikeys) scoped to push the `DuckRun.*` glob.

That's it for nuget.org. The GitHub Packages path uses `secrets.GITHUB_TOKEN`, which GitHub Actions provides automatically — no setup needed.

### Branch protection (recommended)

Without this, anyone with `workflow_dispatch` permission could manually publish from any branch. Each workflow has a guard step that refuses non-master refs, so an unauthorized publish fails fast — but belt-and-braces:

1. Settings → Branches → add a rule for `master`.
2. Tick *Restrict who can push to matching branches* and limit it to maintainers.
3. Tick *Require status checks to pass before merging* if you want PR builds first (no PR-build workflow ships in this repo yet).

## How to publish

### A new stable version to nuget.org

1. Make sure `master` has the code you want shipped.
2. Go to **Actions** → pick e.g. *Publish DuckRun.Core* → **Run workflow**.
3. Pick the `master` branch in the dropdown.
4. Optionally set the `version` input (e.g. `2.0.0` for a major bump). Leave blank to auto-bump the patch.
5. Run. The workflow packs, pushes to nuget.org, and tags the release as `core-v<version>`.

### A prerelease to GitHub Packages for local testing

1. Push the change to the `test` branch (it can fast-forward from `dev`, or be a target branch for ad-hoc test PRs — your call).
2. The relevant workflow auto-fires based on path filters.
3. The resulting prerelease (`1.0.3-test.42`) lands at `https://nuget.pkg.github.com/<owner>/index.json`.
4. Consume it locally by adding the GitHub source to your NuGet feed list — add this to your `~/.nuget/NuGet/NuGet.Config` or to a local `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="github-duckrun" value="https://nuget.pkg.github.com/<owner>/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github-duckrun>
      <add key="Username" value="<your-github-username>" />
      <add key="ClearTextPassword" value="<a-personal-access-token-with-read:packages>" />
    </github-duckrun>
  </packageSourceCredentials>
</configuration>
```

Personal access tokens for the consumer side need `read:packages` scope (and `repo` if the repo is private).

## Conventions baked in

- **Per-package independence.** Bumping `DuckRun.Core` doesn't move any other package's version. The tags (`core-v*`, `efcore-v*`, …) are namespaced.
- **Symbol packages.** `Directory.Build.props` sets `IncludeSymbols=true` and `SymbolPackageFormat=snupkg`, so each push uploads a `.snupkg` alongside the `.nupkg`.
- **Source link & deterministic builds.** `EmbedUntrackedSources=true` and `ContinuousIntegrationBuild=true` (set on the pack step) make for reproducible artifacts.
- **`--skip-duplicate`** on push, so re-running a workflow with the same version is a no-op rather than an error.
- **Path filtering** for the `test` branch — pushing changes to `NuGet - DuckRun.Core/**` only triggers the core workflow, not all four. Framework also re-fires on `NuGet - DuckRun.Core/**` because it links sources from there.
