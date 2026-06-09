## Control Dashboard v__VERSION__

A self-contained release of the DuckRun Control Dashboard — the React SPA and the
ASP.NET Core 10 backend, built and bundled together as a single app.

### Container image (Docker / Kubernetes)

```
docker pull __IMAGE_REF__
```

Also published as `:latest`.

### Downloads

| Asset | Use it for |
|---|---|
| `duckrun-dashboard-__VERSION__-docker-compose.zip` | One-command Docker Compose stack (dashboard + Postgres). |
| `duckrun-dashboard-__VERSION__-kubernetes.zip` | Kubernetes manifests (namespace, Postgres, Deployment, Service, Ingress). |
| `duckrun-dashboard-__VERSION__-iis-framework-dependent.zip` | Windows / IIS — the "pure DLLs". Needs the .NET 10 ASP.NET Core Hosting Bundle on the server. |
| `duckrun-dashboard-__VERSION__-iis-selfcontained-win-x64.zip` | Windows / IIS — bundles the .NET runtime, no install needed (IIS still needs the ASP.NET Core Module). |
| `SHA256SUMS.txt` | Checksums for the archives above. |

Each archive contains a `README.md` / `HOSTING-IIS.md` with step-by-step instructions.

### Quick start — Docker Compose

1. Unzip the docker-compose bundle.
2. `cp .env.example .env` and fill in the secrets.
3. `docker compose up -d` → open http://localhost:8080

### Quick start — Kubernetes

1. Unzip the kubernetes bundle.
2. Copy `duckrun-secrets.example.yaml` → `duckrun-secrets.yaml`, fill in values.
3. `kubectl apply -f duckrun-namespace.yaml` then apply the rest (see the bundle README).

### ⚠️ Heads-up: gRPC ingest is not available over IIS

Connected apps report runs and logs to the dashboard over **gRPC (HTTP/2)**,
which does not work through IIS. The IIS builds serve the UI, REST API, and the
SignalR live console just fine, but to actually ingest data you must expose the
gRPC endpoint via Kestrel directly — see `HOSTING-IIS.md` inside the IIS
archives. The Docker and Kubernetes builds have no such limitation.

### Configuration

Configured via environment variables (double-underscore convention) —
`DuckRun__DashboardSecret`, `DuckRun__Db__Provider`, `DuckRun__Db__ConnectionString`,
`DuckRun__Auth__InitialAdminEmail`, `DuckRun__Auth__InitialAdminPassword`,
`DuckRun__PublicBaseUrl`. See the repository README for the full reference.
