# GitHub Actions — publish & release pipelines

Two pipelines live here: the **NuGet package** publish flow (below) and the
**Control Dashboard** release flow (`release-dashboard.yml`, documented at the end).

## NuGet packages

One orchestrator drives all four packages:

| File | Role |
|---|---|
| `publish.yml` | Entry point. Runs on push to `test`/`master` (and manual dispatch). Detects which packages changed, computes versions, runs tests, then fans out to one publish job per changed package. |
| `_publish-package.yml` | Reusable job called once per package. Packs and pushes to the right feed, and tags the release on `master`. |

The packages and their tag namespaces:

| Package | Tag prefix |
|---|---|
| `DuckRun.Core` | `core-v*` |
| `DuckRun.EfCore` | `efcore-v*` |
| `DuckRun.Redis` | `redis-v*` |
| `DuckRun.Framework` | `framework-v*` |

Each package counts its own version from its own tags.

## Branch flow

```
dev  --merge-->  test  --merge-->  master
                  |                   |
            nugettest.org         nuget.org
            (prerelease)        (stable + tag)
```

| Branch | Trigger | Destination | Version scheme |
|---|---|---|---|
| `test` | push / merge from `dev` | **apiint.nugettest.org** | `<next-patch>-test.<run_number>` (prerelease) |
| `master` | push / merge from `test` | **nuget.org** | `<next-patch>` stable; also pushes a `<prefix>-v<version>` git tag |
| `dev` (and others) | — | — | nothing fires |

`master` is the production branch. Merging into it publishes stable packages automatically — no manual button required.

## Only the changed packages publish (dependency-aware)

On a push, the `changes` job diffs the pushed range and decides which packages to publish. It is **dependency-aware**, but only fans out where a rebuild is genuinely required:

- `DuckRun.Framework` **compiles `DuckRun.Core`'s source files directly** (`<Compile Include>` from Core), so it **must** rebuild whenever Core changes. Core therefore fans out to Framework.
- `DuckRun.EfCore` and `DuckRun.Redis` reference `DuckRun.Core` / `DuckRun.Framework` as ordinary **minimum-version** NuGet dependencies. Their own bits don't change when an upstream package changes, and a consumer can upgrade the upstream package independently — so they are **not** rebuilt on an upstream change.

So the fan-out is:

| You changed | Published |
|---|---|
| `NuGet - DuckRun.Core/**` | Core, Framework |
| `NuGet - DuckRun.Framework/**` | Framework |
| `NuGet - DuckRun.EfCore/**` | EfCore |
| `NuGet - DuckRun.Redis/**` | Redis |
| `Directory.Build.props` | all four |

Editing *only* Redis publishes *only* Redis — exactly as expected.

## Dependency versions are pinned independently

Each package has its own version line, so a package's own version and the version it
*depends on* must be decoupled — otherwise publishing only Redis would emit a Redis that
depends on a Redis-numbered Core that was never pushed.

The `versions` job computes, for every package, its **effective version**:

- in the publish set → its new version (next patch, or prerelease on `test`);
- not in the set → its **latest published version** (the floor others should depend on).

These are passed to every `dotnet pack` as `-p:DuckRun<Pkg>Version=…`. Because each csproj
reads only its own `DuckRun<Pkg>Version` property (a no-op when unset, so local builds are
unaffected), the dependency a package emits is pinned to a version that actually exists on
the feed. Verified: packing Redis at `9.9.9` with Core pinned to `1.2.3` emits a Redis
`9.9.9` whose `net10.0` group depends on `DuckRun.Core 1.2.3`.

## Tests gate every publish

The `test` job runs `dotnet test` on `Tests - DuckRun` and must pass before any publish job
starts (`needs: [changes, versions, test]`). A failing test blocks the release.

## Required setup

### Two repository secrets

- **`NUGET_API_KEY`** — a [nuget.org API key](https://www.nuget.org/account/apikeys) scoped to push `DuckRun.*`. Used on `master`.
- **`NUGET_TEST_API_KEY`** — an API key from the **test gallery** at <https://int.nugettest.org/account/apikeys>. Used on `test`. Accounts on nugettest.org are separate from nuget.org — register there once. (The test gallery is wiped periodically; treat it as throwaway.)

### The `test` branch

Create it once (it doesn't exist yet):

```bash
git switch -c test master && git push -u origin test
```

Then the flow is: work on `dev` → merge `dev`→`test` (publishes prereleases) → merge `test`→`master` (publishes stable).

### Branch protection (recommended)

1. Settings → Branches → protect `master` (restrict who can push / require PRs).
2. Optionally require the `test` status check to pass before merging into `master`.

> First-ever release: run the manual dispatch with **package = all** (or push all four together) so every package exists at `1.0.0` before any partial publish depends on it.

## Manual publish (workflow_dispatch)

Actions → **Publish DuckRun packages** → **Run workflow**:

- **package**: `all` or a single package.
- **version**: explicit version for the selected package(s); blank = auto-increment patch.
- The **branch you run from** decides the feed: `master` → nuget.org, `test` → nugettest.org.

## Conventions baked in

- **Per-package independence.** Bumping one package doesn't move another's version; tags are namespaced (`core-v*`, `efcore-v*`, …).
- **Symbol packages.** `Directory.Build.props` sets `IncludeSymbols=true` / `SymbolPackageFormat=snupkg`, so a `.snupkg` ships alongside each `.nupkg`.
- **Deterministic builds.** `EmbedUntrackedSources=true` and `ContinuousIntegrationBuild=true` (on the pack step) for reproducible artifacts.
- **`--skip-duplicate`** on push, so re-running with an already-published version is a no-op.

---

# Control Dashboard release

`release-dashboard.yml` ships the **Control Dashboard** (the SPA + backend bundled as one app). It's independent of the NuGet flow above.

| | |
|---|---|
| **Trigger** | Push / merge into `master` touching the dashboard (backend, frontend, `NuGet - DuckRun.Core/EfCore`, `deploy/**`), plus manual dispatch. |
| **What it does** | Builds the SPA into the backend `wwwroot`, publishes the backend two ways, builds & pushes a container image to GHCR, and opens a **draft** GitHub Release. |
| **Version** | Auto-increments the patch from the latest `dashboard-v*` tag (or `0.1.0` the first time); override via the dispatch `version` input. The git tag is created when you publish the draft. |

## Downloads attached to each release

- `…-iis-framework-dependent.zip` — "pure DLLs" for IIS (server needs the .NET 10 ASP.NET Core Hosting Bundle).
- `…-iis-selfcontained-win-x64.zip` — IIS build with the .NET runtime bundled.
- `…-docker-compose.zip` — dashboard + Postgres stack.
- `…-kubernetes.zip` — namespace, Postgres, Deployment, Service, Ingress.
- `SHA256SUMS.txt`.

Container image: `ghcr.io/<owner>/duckrun-dashboard:<version>` (and `:latest`).

## To cut a release

Merge `test` → `master`. The workflow builds everything and leaves a **draft** release under *Releases*. Review it, then click **Publish** — that's also when the `dashboard-v<version>` git tag is created.

## One-time setup

- **GHCR visibility:** the first run creates the `duckrun-dashboard` package as *private*. Make it public (Packages → the package → *Package settings* → *Change visibility*) so `docker pull` works without auth. No secret needed — the workflow authenticates with the built-in `GITHUB_TOKEN`.
- **gRPC + IIS caveat:** the dashboard's ingest endpoint is gRPC (HTTP/2), which does not work through IIS. The IIS downloads serve the UI / REST / SignalR; ingest needs Kestrel directly (Docker / Kubernetes have no limitation). The release notes and the IIS archive's `HOSTING-IIS.md` explain the workaround.
