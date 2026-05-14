# DuckRun

A modern background job scheduler for ASP.NET Core and ASP.NET (Framework), with a centralized multi-project control dashboard.

> **Status — pre-release.** DuckRun is under active development on the road to a 1.0. The API surface, package layout, and dashboard endpoints in this README represent the target shape of the project. Track progress in the [Roadmap](#roadmap).

---

## Table of contents

- [Why DuckRun](#why-duckrun)
- [Features](#features)
- [Architecture](#architecture)
- [Installation](#installation)
- [Quick start (modern .NET)](#quick-start-modern-net)
- [Quick start (.NET Framework)](#quick-start-net-framework)
- [Persistence — DuckRun.EfCore](#persistence--duckrunefcore)
- [Multi-instance — DuckRun.Redis](#multi-instance--duckrunredis)
- [Console logging](#console-logging)
- [Cancellation](#cancellation)
- [Dashboard](#dashboard)
  - [Standalone (embedded) dashboard](#standalone-embedded-dashboard)
  - [Centralized Control Dashboard](#centralized-control-dashboard)
  - [Docker](#docker)
  - [Docker Compose](#docker-compose)
  - [Kubernetes](#kubernetes)
  - [Configuration reference](#dashboard-configuration-reference)
- [DSN format](#dsn-format)
- [Compatibility matrix](#compatibility-matrix)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## Why DuckRun

Hangfire and TickerQ both work, but each has rough edges: legacy code paths, opinionated storage, awkward multi-tenant stories, dashboard auth that doesn't fit modern setups. DuckRun is a clean-slate scheduler with:

- A single, attribute-driven job registration model
- First-class cancellation tokens via standard DI
- A separate, standalone Control Dashboard that aggregates **many** apps in one place over a Sentry-style DSN
- Native support for both modern .NET (net6 through net10) **and** classic ASP.NET on .NET Framework 4.8 — including persistence and multi-instance orchestration on both runtimes
- Pluggable persistence (PostgreSQL, SQL Server, MySQL/MariaDB, CockroachDB)
- Pluggable cluster coordination via Redis (leader election + distributed concurrency limits)

## Features

- Attribute-based job declaration (`[DuckRunJob]`) with cron, concurrency, timeout, and manual-trigger flags
- DST-safe cron via [Cronos](https://github.com/HangfireIO/Cronos)
- Per-run DI scope and `CancellationToken` injection
- Built-in in-job console logging (persisted to storage, streamed live to dashboard)
- Manual trigger and cancel from dashboard or code
- Optional persistence — survives restarts, replays state on boot
- Optional Redis leader election — only one instance fires each tick across a web farm or Kubernetes deployment
- Two dashboard modes:
  - **Standalone**: minimal embedded UI inside your app at `/duckrun`
  - **Centralized**: separate Docker-hosted Control Dashboard, connect many apps via DSN
- No CORS pain — the Control Dashboard ships frontend and backend in the same container

## Architecture

```
+-------------------------------+        +-------------------------------+
|  Host app (net6..net10)       |        |  Host app (net48 / classic)   |
|  + DuckRun.Core               |        |  + DuckRun.Framework          |
|     - Discovery + scheduler   |        |     - Same job model          |
|     - Cancellation, console   |        |     - net48-compatible DI     |
+----+--------+-----------------+        +----+--------+-----------------+
     |        |                                |        |
     |        v (optional)                     |        v (optional)
     |  +-----------+   +-------------+        |  +-----------+   +-------------+
     |  | .EfCore   |   | .Redis      |        |  | .EfCore   |   | .Redis      |
     |  | history,  |   | leader,     |        |  | history,  |   | leader,     |
     |  | logs,     |   | concurrency |        |  | logs,     |   | concurrency |
     |  | schedules |   | (cluster)   |        |  | schedules |   | (web farm)  |
     |  +-----------+   +-------------+        |  +-----------+   +-------------+
     |                                         |
     | DSN (HTTPS ingest + SignalR commands)   |
     +-----------------+-----------------------+
                       |
                       v
       +-------------------------------------+
       |  DuckRun Control Dashboard          |
       |  (one Docker container)             |
       |   - React SPA served from wwwroot   |
       |   - ASP.NET Core MVC + API + Hub    |
       |   - Multi-tenant: many projects     |
       |   - Postgres for dashboard data     |
       +-------------------------------------+
```

The four DuckRun NuGet packages compose as you need them:

| Package | Purpose |
|---------|---------|
| `DuckRun.Core` | Job scheduler for modern .NET (net6 through net10) |
| `DuckRun.Framework` | Same scheduler for classic ASP.NET on .NET Framework 4.8 |
| `DuckRun.EfCore` | Persistence: job history, console logs, schedules. Modern targets use EF Core; net48 uses Entity Framework 6 — same public API |
| `DuckRun.Redis` | Multi-instance coordination: leader election and distributed concurrency limits |

`.EfCore` and `.Redis` are independent — use either, both, or neither. Both packages support modern .NET and net48.

---

## Installation

All packages are published to [nuget.org](https://www.nuget.org) under the `DuckRun.*` namespace.

```bash
# Modern .NET (net6 - net10)
dotnet add package DuckRun.Core

# Optional add-ons
dotnet add package DuckRun.EfCore
dotnet add package DuckRun.Redis
```

On .NET Framework 4.8:

```powershell
# Package Manager Console
Install-Package DuckRun.Framework
Install-Package DuckRun.EfCore     # optional
Install-Package DuckRun.Redis      # optional
```

---

## Quick start (modern .NET)

**Register DuckRun in `Program.cs`:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDuckRun(o =>
{
    // Scan this assembly for [DuckRunJob] methods
    o.AddJobsFromAssembly(typeof(Program).Assembly);

    // Optional: persistence (replays state across restarts)
    o.UseEfCore(builder.Configuration.GetConnectionString("DuckRun"),
                DuckRunProvider.Postgres);

    // Optional: multi-instance orchestration
    o.UseRedis(builder.Configuration.GetConnectionString("Redis"));

    // Centralized dashboard: connect via DSN
    o.UseDashboard("https://abc123@dashboard.example.com/42");

    // ...or, if you want the embedded runtime UI instead:
    // o.UseStandaloneDashboard();
});

var app = builder.Build();

app.MapDuckRunDashboard(); // only needed for the standalone dashboard
app.Run();
```

**Declare a job — anywhere in your app:**

```csharp
public class ReportingJobs
{
    private readonly IReportService _reports;

    public ReportingJobs(IReportService reports) => _reports = reports;

    [DuckRunJob("daily-revenue", "0 2 * * *", MaxConcurrency = 1)]
    public async Task GenerateDailyRevenue(IDuckRunConsole console, CancellationToken ct)
    {
        console.Info("Starting daily revenue report");
        await _reports.GenerateRevenueAsync(ct);
        console.Info("Done");
    }
}
```

DuckRun discovers the method at startup, registers the cron, and runs it in a fresh DI scope each tick. Manual triggering, cancellation, and run history are available via the dashboard.

---

## Quick start (.NET Framework)

For classic ASP.NET on net48, wire DuckRun up in `Global.asax.cs`:

```csharp
public class MvcApplication : HttpApplication
{
    protected void Application_Start()
    {
        AreaRegistration.RegisterAllAreas();
        RouteConfig.RegisterRoutes(RouteTable.Routes);

        DuckRunHost.Start(o =>
        {
            o.AddJobsFromAssembly(typeof(MvcApplication).Assembly);

            // Same persistence and Redis options as modern .NET
            o.UseEfCore("Server=...;Database=...;", DuckRunProvider.SqlServer);
            o.UseRedis("redis-1:6379,redis-2:6379");

            o.UseDashboard("https://abc123@dashboard.example.com/42");
        });
    }

    protected void Application_End()
    {
        DuckRunHost.Stop();
    }
}
```

Jobs are declared exactly the same way as on modern .NET, with `[DuckRunJob]`. If you're using a third-party container (Autofac, Unity, Ninject), pass it via `o.UseContainer(...)` so DuckRun resolves job classes from your DI graph.

`DuckRun.EfCore` on net48 transparently uses Entity Framework 6 under the hood, with the same `UseEfCore(connString, provider)` API — no code change required when porting between runtimes.

---

## Persistence — `DuckRun.EfCore`

Without `.EfCore`, run history and console logs live in memory only. Add it once and DuckRun:

- Records every job run (start, end, status, exception)
- Stores in-job console output
- Replays the last known state on boot — schedules continue, "stuck running" jobs are marked failed
- Lets the dashboard show history that survives restarts

**Configuration:**

```csharp
o.UseEfCore(connectionString, DuckRunProvider.Postgres);
```

Supported providers:

| Provider | `DuckRunProvider` value | Driver |
|----------|-------------------------|--------|
| PostgreSQL | `Postgres` | Npgsql |
| CockroachDB | `CockroachDb` | Npgsql (Cockroach wire-compatible) |
| SQL Server | `SqlServer` | Microsoft.Data.SqlClient (modern) / System.Data.SqlClient (net48) |
| MySQL / MariaDB | `MySql` | Pomelo (modern) / MySql.Data (net48) |

The first time DuckRun boots against a fresh database it creates a `DuckRun` schema (or `DuckRun_`-prefixed tables on MySQL, which has no schemas) with the tables it needs. The DDL is idempotent — no `dotnet ef migrations` step required.

---

## Multi-instance — `DuckRun.Redis`

If your app runs in a Kubernetes deployment, an IIS web farm, or any setup with more than one process, you don't want every instance firing every cron tick. Add `.Redis` and DuckRun coordinates:

- **Leader election** — exactly one instance per project drives the scheduler at any moment, via a TTL-renewed Redis key. If the leader dies, a new one is elected within seconds.
- **Distributed concurrency** — a job with `MaxConcurrency = 3` will never have more than 3 in-flight runs across the entire cluster.
- **Heartbeats** — each node reports liveness; the dashboard shows the cluster view.

**Configuration:**

```csharp
o.UseRedis("redis-1:6379,redis-2:6379,redis-3:6379");
```

Connection strings follow the standard StackExchange.Redis format. Sentinel and cluster modes are supported.

---

## Console logging

Inject `IDuckRunConsole` into any job method (or any service it calls — it's scoped to the running job):

```csharp
[DuckRunJob("import", "*/15 * * * *")]
public async Task Import(IDuckRunConsole console, CancellationToken ct)
{
    console.Info("Fetching feed");
    var rows = await _feed.PullAsync(ct);

    console.Info($"Pulled {rows.Count} rows");
    foreach (var row in rows)
    {
        try { await _db.UpsertAsync(row, ct); }
        catch (Exception ex) { console.Warning($"Row {row.Id} failed: {ex.Message}"); }
    }

    console.Info("Done");
}
```

Logs appear live in the dashboard, are persisted by `.EfCore`, and are attached to the run record. A static `DuckRunConsole.Current` accessor is also available for code paths where DI injection is awkward.

---

## Cancellation

Job methods receive a standard `CancellationToken`. The token is signalled when:

- The host app is shutting down
- An operator cancels the run from the dashboard
- The job's `TimeoutSeconds` is exceeded

Always honour the token — pass it down to your async calls. DuckRun does not abort threads.

---

## Dashboard

DuckRun has two dashboard options. Pick one based on how much visibility you need.

### Standalone (embedded) dashboard

A minimal Razor UI shipped inside `DuckRun.Core` (and `.Framework`). Enable it and visit `/duckrun`:

```csharp
o.UseStandaloneDashboard();
// ...
app.MapDuckRunDashboard(); // modern .NET
```

What it gives you:

- List of registered jobs with next-run times
- Recent run history (in-memory by default, persistent if `.EfCore` is enabled)
- Live job console
- Manual trigger and cancel buttons

Best for a single app where you only need a quick local view. For a cluster, use it on each node — but each only sees its own state.

### Centralized Control Dashboard

A separate ASP.NET Core MVC application that aggregates **many** DuckRun-instrumented apps under one roof. You create projects in the dashboard, each gets a DSN, you paste the DSN into your app's config, done.

What you get:

- One dashboard for 1 to N apps
- Per-project view of all jobs, schedules, runs
- Live job console streamed via SignalR
- Manual trigger and cancel routed back to the originating node
- Cluster view per project (which nodes are alive, which is leader)
- Multi-user auth (cookie-based local accounts in v1; OIDC on the roadmap)

#### Docker

The dashboard is published as a single image on the GitHub Container Registry:

```
ghcr.io/<owner>/duckrun-dashboard:latest
```

Replace `<owner>` with the GitHub organisation/user that hosts the repo. (This README will be updated with the final image coordinates at 1.0.)

Quickest possible spin-up — Postgres bundled, ephemeral, for evaluation only:

```bash
docker run --rm -p 8080:8080 \
  -e DuckRun__DashboardSecret="change-me" \
  -e DuckRun__Db__ConnectionString="Host=db;Database=duckrun;Username=duckrun;Password=duckrun" \
  -e DuckRun__Db__Provider="Postgres" \
  ghcr.io/<owner>/duckrun-dashboard:latest
```

You'll need to point it at a real Postgres for anything beyond a five-minute kick of the tires — use Compose or Kubernetes for that.

#### Docker Compose

Save this as `docker-compose.yml`:

```yaml
services:
  duckrun-dashboard:
    image: ghcr.io/<owner>/duckrun-dashboard:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_URLS: http://+:8080
      DuckRun__DashboardSecret: ${DASHBOARD_SECRET}
      DuckRun__Db__Provider: Postgres
      DuckRun__Db__ConnectionString: Host=postgres;Port=5432;Database=duckrun;Username=duckrun;Password=${POSTGRES_PASSWORD}
      DuckRun__Auth__InitialAdminEmail: ${ADMIN_EMAIL}
      DuckRun__Auth__InitialAdminPassword: ${ADMIN_PASSWORD}
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:16
    restart: unless-stopped
    environment:
      POSTGRES_DB: duckrun
      POSTGRES_USER: duckrun
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - duckrun-pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U duckrun -d duckrun"]
      interval: 5s
      timeout: 5s
      retries: 10

volumes:
  duckrun-pgdata:
```

Create a `.env` next to it:

```env
DASHBOARD_SECRET=replace-with-a-long-random-string
POSTGRES_PASSWORD=replace-with-a-strong-password
ADMIN_EMAIL=you@example.com
ADMIN_PASSWORD=replace-with-a-strong-admin-password
```

Then:

```bash
docker compose up -d
```

The dashboard is at `http://localhost:8080`. Log in with the initial admin credentials, create your first project, and copy the DSN into your app.

#### Kubernetes

Minimal manifests to get going. Adjust storage class, ingress controller, and TLS issuer to match your cluster.

**`duckrun-secrets.yaml`** — DO NOT commit real secrets to git; use Sealed Secrets, SOPS, or your secret manager.

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: duckrun-dashboard
  namespace: duckrun
type: Opaque
stringData:
  dashboard-secret: "replace-with-a-long-random-string"
  postgres-password: "replace-with-a-strong-password"
  admin-email: "you@example.com"
  admin-password: "replace-with-a-strong-admin-password"
```

**`duckrun-postgres.yaml`** — minimal StatefulSet for evaluation. For production, prefer a managed Postgres or an operator (CloudNativePG, Zalando):

```yaml
apiVersion: v1
kind: Service
metadata:
  name: duckrun-postgres
  namespace: duckrun
spec:
  selector:
    app: duckrun-postgres
  ports:
    - port: 5432
  clusterIP: None
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: duckrun-postgres
  namespace: duckrun
spec:
  serviceName: duckrun-postgres
  replicas: 1
  selector:
    matchLabels:
      app: duckrun-postgres
  template:
    metadata:
      labels:
        app: duckrun-postgres
    spec:
      containers:
        - name: postgres
          image: postgres:16
          env:
            - name: POSTGRES_DB
              value: duckrun
            - name: POSTGRES_USER
              value: duckrun
            - name: POSTGRES_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: duckrun-dashboard
                  key: postgres-password
          ports:
            - containerPort: 5432
          volumeMounts:
            - name: data
              mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
    - metadata:
        name: data
      spec:
        accessModes: ["ReadWriteOnce"]
        resources:
          requests:
            storage: 10Gi
```

**`duckrun-dashboard.yaml`**:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: duckrun-dashboard
  namespace: duckrun
spec:
  replicas: 1
  selector:
    matchLabels:
      app: duckrun-dashboard
  template:
    metadata:
      labels:
        app: duckrun-dashboard
    spec:
      containers:
        - name: dashboard
          image: ghcr.io/<owner>/duckrun-dashboard:latest
          ports:
            - containerPort: 8080
          env:
            - name: ASPNETCORE_URLS
              value: http://+:8080
            - name: DuckRun__Db__Provider
              value: Postgres
            - name: DuckRun__Db__ConnectionString
              value: Host=duckrun-postgres;Port=5432;Database=duckrun;Username=duckrun;Password=$(POSTGRES_PASSWORD)
            - name: POSTGRES_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: duckrun-dashboard
                  key: postgres-password
            - name: DuckRun__DashboardSecret
              valueFrom:
                secretKeyRef:
                  name: duckrun-dashboard
                  key: dashboard-secret
            - name: DuckRun__Auth__InitialAdminEmail
              valueFrom:
                secretKeyRef:
                  name: duckrun-dashboard
                  key: admin-email
            - name: DuckRun__Auth__InitialAdminPassword
              valueFrom:
                secretKeyRef:
                  name: duckrun-dashboard
                  key: admin-password
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 15
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
          resources:
            requests:
              cpu: 100m
              memory: 256Mi
            limits:
              cpu: 1000m
              memory: 1Gi
---
apiVersion: v1
kind: Service
metadata:
  name: duckrun-dashboard
  namespace: duckrun
spec:
  selector:
    app: duckrun-dashboard
  ports:
    - port: 80
      targetPort: 8080
```

**`duckrun-ingress.yaml`** — example with cert-manager and nginx-ingress:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: duckrun-dashboard
  namespace: duckrun
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"   # SignalR keep-alive
    nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - duckrun.example.com
      secretName: duckrun-tls
  rules:
    - host: duckrun.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: duckrun-dashboard
                port:
                  number: 80
```

Apply:

```bash
kubectl create namespace duckrun
kubectl apply -f duckrun-secrets.yaml
kubectl apply -f duckrun-postgres.yaml
kubectl apply -f duckrun-dashboard.yaml
kubectl apply -f duckrun-ingress.yaml
```

> **SignalR note** — the dashboard streams live logs and command traffic over SignalR (WebSockets). Make sure your ingress controller is configured to allow long-lived connections (the `proxy-read-timeout` annotation above does this for nginx). Behind Cloudflare or similar, WebSockets must be enabled.

A Helm chart is on the roadmap.

#### Dashboard configuration reference

The dashboard reads configuration from environment variables (and `appsettings.json`). Variables use the standard ASP.NET Core double-underscore convention.

| Variable | Required | Description |
|----------|:--------:|-------------|
| `DuckRun__DashboardSecret` | yes | Random string used to sign DSN public keys and session cookies. Rotate to invalidate all DSNs. |
| `DuckRun__Db__Provider` | yes | `Postgres`, `SqlServer`, or `MySql`. (CockroachDB uses `Postgres`.) |
| `DuckRun__Db__ConnectionString` | yes | ADO.NET-style connection string. |
| `DuckRun__Auth__InitialAdminEmail` | first boot | Created on first start; ignored on subsequent boots. |
| `DuckRun__Auth__InitialAdminPassword` | first boot | Same — change after first login. |
| `DuckRun__PublicBaseUrl` | recommended | The externally-reachable URL of the dashboard. DSNs the dashboard issues will use this host. |
| `DuckRun__Ingest__MaxPayloadKb` | no | Default 256. Per-request ingest payload cap. |
| `DuckRun__Retention__RunDays` | no | Default 30. Run records older than this are pruned. |
| `DuckRun__Retention__ConsoleDays` | no | Default 7. Console log retention. |
| `ASPNETCORE_URLS` | no | Defaults to `http://+:8080` inside the container. |

---

## DSN format

DuckRun uses a Sentry-style DSN to connect host apps to the Control Dashboard:

```
https://<publicKey>@<dashboard-host>/<projectId>
```

The host app uses the public key to authenticate ingest (HTTPS POSTs) and to subscribe to the SignalR command hub. Each project in the dashboard has its own DSN; rotate it from the project's Settings page and the old key stops working immediately.

DSNs are issued only by the dashboard — your app never invents one.

---

## Compatibility matrix

| Package | net48 | net6.0 | net7.0 | net8.0 | net9.0 | net10.0 |
|---------|:-:|:-:|:-:|:-:|:-:|:-:|
| `DuckRun.Core` | — | yes | yes | yes | yes | yes |
| `DuckRun.Framework` | yes | — | — | — | — | — |
| `DuckRun.EfCore` | yes (EF6) | yes (EF Core) | yes (EF Core) | yes (EF Core) | yes (EF Core) | yes (EF Core) |
| `DuckRun.Redis` | yes | yes | yes | yes | yes | yes |
| Control Dashboard image | runs as Docker container — host runtime irrelevant to your app |

Supported databases (via `.EfCore`, either runtime):

- PostgreSQL 13+
- CockroachDB 23.1+
- SQL Server 2017+ / Azure SQL
- MySQL 8.0+ / MariaDB 10.6+

Supported Redis: 6.2+ (standalone, Sentinel, or Cluster).

---

## Roadmap

- [ ] Phase 0 — solution scaffolding + shared metadata
- [ ] Phase 1 — `DuckRun.Core` MVP (in-memory scheduler + standalone dashboard)
- [ ] Phase 2 — `DuckRun.EfCore` (modern .NET via EF Core)
- [ ] Phase 3 — `DuckRun.Redis` (leader election + distributed concurrency)
- [ ] Phase 4 — Control Dashboard backend
- [ ] Phase 5 — Control Dashboard frontend
- [ ] Phase 6 — Wire `.Core` to Control Dashboard over DSN
- [ ] Phase 7 — `DuckRun.Framework` net48 port (+ EF6-backed `.EfCore` net48 target)
- [ ] Phase 8 — GitHub Actions: NuGet publish pipelines + dashboard image to GHCR
- [ ] Phase 9 — Polish: per-package READMEs, sample apps, integration tests
- [ ] Post-1.0 — Helm chart, OIDC/SSO for the dashboard, alerting hooks (webhooks, Slack, email)

## Contributing

Issues and PRs are welcome once the project hits a public preview. Until then the codebase is changing daily — open an issue first to discuss a contribution.

## License

MIT. See [`LICENSE`](LICENSE).
