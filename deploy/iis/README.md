# Control Dashboard — IIS hosting (Windows)

This archive is a published build of the DuckRun Control Dashboard (the React SPA
is already baked into `wwwroot`). Two flavors are published per release:

- **framework-dependent** — the "pure DLLs" build. Smallest. Requires the
  **.NET 10 ASP.NET Core Hosting Bundle** on the server.
- **selfcontained-win-x64** — bundles the .NET runtime, so no .NET install is
  needed. IIS still needs the ASP.NET Core Module (see Prerequisites).

## ⚠️ Read this first: gRPC ingest does NOT work through IIS

Your apps report job runs and logs to the dashboard over **gRPC (HTTP/2)**. IIS
cannot serve gRPC — it doesn't support the HTTP/2 response trailers gRPC
requires, and this is true for **both** the in-process and out-of-process
ASP.NET Core hosting models.

What this means for an IIS deployment:

- ✅ The dashboard **UI**, the **REST API**, and the **SignalR** live console work fine behind IIS.
- ❌ The **gRPC ingest endpoint will not work** behind IIS — connected apps cannot push runs/logs to it.

If you need ingest (you almost certainly do), pick one of:

1. **Use the Docker or Kubernetes build instead.** They have no gRPC limitation — this is the recommended path.
2. **Run the dashboard on Kestrel directly** (a Windows Service or a console process) on its own port for ingest, and optionally keep IIS only as a reverse proxy for the HTTP/1.1 (UI/REST) traffic. The gRPC port must reach Kestrel **without** passing through IIS.
3. **Use IIS for the UI only** and accept that no data is ingested (rarely useful on its own).

The rest of this guide covers standard IIS hosting for the UI / REST / SignalR surface.

## Prerequisites

1. **IIS** with these features enabled (Server Manager → *Add Roles and Features*, or `Enable-WindowsOptionalFeature`):
   - Web Server (IIS)
   - **WebSocket Protocol** — required for the SignalR live console.
2. **ASP.NET Core Module v2 (ANCM):**
   - *framework-dependent* build → install the **.NET 10 ASP.NET Core Hosting Bundle** (it installs the runtime *and* the module).
   - *self-contained* build → you still need the module; installing the Hosting Bundle is the simplest way to get it (the bundled runtime just goes unused).
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
3. After installing the Hosting Bundle, restart IIS: `net stop was /y && net start w3svc`.

## Install

1. Unzip into a folder, e.g. `C:\inetpub\duckrun-dashboard`.
2. In IIS Manager, create (or reuse) an **Application Pool** with **.NET CLR version: No Managed Code** (the app runs via ANCM, not the .NET Framework CLR).
3. Add a Website (or Application) whose physical path is the unzipped folder, using that App Pool.
4. Give the App Pool identity read access to the folder (and write access to any log folder you enable).

A `web.config` is already in the archive — `dotnet publish` generates it with the
correct ASP.NET Core Module handler. You usually only edit it to add configuration (below).

## Configuration

The dashboard reads configuration from environment variables (double-underscore
convention) or `appsettings.json`. The cleanest way on IIS is to add environment
variables to `web.config` inside the `<aspNetCore>` element:

```xml
<aspNetCore processPath="dotnet" arguments=".\DuckRun.Dashboard.dll" stdoutLogEnabled="false" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="DuckRun__DashboardSecret" value="replace-with-a-long-random-string" />
    <environmentVariable name="DuckRun__Db__Provider" value="SqlServer" />
    <environmentVariable name="DuckRun__Db__ConnectionString" value="Server=.;Database=DuckRun;Trusted_Connection=True;TrustServerCertificate=True" />
    <environmentVariable name="DuckRun__Auth__InitialAdminEmail" value="you@example.com" />
    <environmentVariable name="DuckRun__Auth__InitialAdminPassword" value="change-on-first-login" />
  </environmentVariables>
</aspNetCore>
```

For the **self-contained** build, `processPath` is `.\DuckRun.Dashboard.exe` with
no `arguments` — the generated `web.config` already reflects this, so only add the
`<environmentVariables>` block.

Supported DB providers: `SqlServer`, `Postgres` (CockroachDB uses `Postgres`).
See the repository README for the full configuration reference.

## Verify

- Browse to the site — you should get the dashboard login.
- `GET /health/live` → `{"status":"live"}`.
- `GET /health/ready` → `{"status":"ready"}` once the database is reachable.

A 500.19 / 500.30 / 500.31 error is almost always a missing Hosting Bundle / ANCM
or a bad connection string. Check Windows Event Viewer (Application log) and
temporarily set `stdoutLogEnabled="true"` in `web.config`.
