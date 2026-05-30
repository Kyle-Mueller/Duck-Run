import { useEffect, useState } from 'react';
import CodeBlock from '../components/CodeBlock';
import { SITE } from '../site';
import styles from './DocsPage.module.css';

const NAV = [
  {
    group: 'Getting started',
    items: [
      { id: 'introduction', label: 'Introduction' },
      { id: 'installation', label: 'Installation' },
    ],
  },
  {
    group: 'Authoring jobs',
    items: [
      { id: 'jobs', label: 'Defining jobs' },
      { id: 'console', label: 'The console' },
      { id: 'cancellation', label: 'Cancellation & timeouts' },
      { id: 'triggers', label: 'Manual triggers' },
    ],
  },
  {
    group: 'Dashboards',
    items: [
      { id: 'standalone', label: 'Embedded /duckrun' },
      { id: 'dashboard', label: 'Control Server & DSN' },
    ],
  },
  {
    group: 'Scaling out',
    items: [
      { id: 'persistence', label: 'Persistence' },
      { id: 'cluster', label: 'Clustering' },
    ],
  },
  {
    group: 'Platforms',
    items: [{ id: 'framework', label: '.NET Framework 4.8' }],
  },
  {
    group: 'Reference',
    items: [
      { id: 'reference', label: 'API surface' },
      { id: 'faq', label: 'FAQ' },
    ],
  },
];

const INSTALL_PROGRAM = `public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDuckRun(o =>
        {
            o.AddJobsFromAssemblyContaining<Program>();

            // optional — pick the ones you need
            o.UseEfCore(builder.Configuration.GetConnectionString("DuckRun"), DuckRunProvider.Postgres);
            o.UseRedis(builder.Configuration.GetConnectionString("Redis"));
            o.UseDashboard("https://pk_live_8f3a@dashboard.example.com/42");

            // or run the embedded single-instance UI
            o.UseStandaloneDashboard("/duckrun");
        });

        var app = builder.Build();
        app.MapDuckRunDashboard();   // only needed with UseStandaloneDashboard

        await app.RunAsync();
    }
}`;

const JOB_EXAMPLE = `public sealed class ImportJobs(IFeedClient feed, IImportStore store)
{
    [DuckRunJob("import-feed", "*/5 * * * *", MaxConcurrency = 1, TimeoutSeconds = 240)]
    public async Task ImportFeed(IDuckRunConsole console, CancellationToken ct)
    {
        console.Info("Pulling the feed…");
        var items = await feed.PullAsync(ct);

        console.Info($"Upserting {items.Count} items");
        await store.UpsertAsync(items, ct);

        console.Info("Done");
    }
}`;

const CONSOLE_EXAMPLE = `[DuckRunJob("nightly-rollup", "0 3 * * *")]
public async Task Rollup(IDuckRunConsole console, CancellationToken ct)
{
    console.Info("Starting rollup");

    try
    {
        await DoWorkAsync(ct);
    }
    catch (Exception ex)
    {
        console.Error($"Rollup failed: {ex.Message}");
        throw;   // still surfaces as a Failed run, with the stack captured
    }
}

// Deep in a helper where DI is awkward — reach the ambient console:
DuckRunConsole.Current?.Warning("Falling back to cached prices");`;

const TRIGGER_EXAMPLE = `app.MapPost("/admin/jobs/{name}/run", async (string name, IDuckRunController duckrun, CancellationToken ct) =>
{
    var runId = await duckrun.TriggerAsync(name, ct);
    return Results.Ok(new { runId });
});

app.MapGet("/admin/runs/{id:guid}/console", async (Guid id, IDuckRunController duckrun, CancellationToken ct) =>
{
    var lines = await duckrun.GetConsoleAsync(id, ct);
    return Results.Ok(lines);
});`;

const GLOBAL_ASAX = `public class MvcApplication : HttpApplication
{
    protected void Application_Start()
    {
        DuckRunHost.Start(o =>
        {
            o.AddJobsFromAssembly(typeof(MvcApplication).Assembly);

            // jobs with constructor dependencies need a factory
            o.UseJobFactory(type => DependencyResolver.Current.GetService(type));

            o.UseRedis("redis-1:6379,redis-2:6379");
        });
    }

    protected void Application_End() => DuckRunHost.Stop();
}`;

const COMPOSE = `services:
  duckrun-dashboard:
    image: ghcr.io/<owner>/duckrun-dashboard:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      DuckRun__DashboardSecret: \${DASHBOARD_SECRET}
      DuckRun__Db__Provider: Postgres
      DuckRun__Db__ConnectionString: Host=postgres;Database=duckrun;Username=duck;Password=\${PG_PASSWORD}
      DuckRun__Auth__InitialAdminEmail: admin@example.com
      DuckRun__Auth__InitialAdminPassword: \${ADMIN_PASSWORD}
      DuckRun__PublicBaseUrl: https://duckrun.example.com
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: duckrun
      POSTGRES_USER: duck
      POSTGRES_PASSWORD: \${PG_PASSWORD}`;

export default function DocsPage() {
  const [active, setActive] = useState('introduction');
  const [navOpen, setNavOpen] = useState(false);

  useEffect(() => {
    const ids = NAV.flatMap((g) => g.items.map((i) => i.id));
    const els = ids
      .map((id) => document.getElementById(id))
      .filter((el): el is HTMLElement => el !== null);

    const obs = new IntersectionObserver(
      (entries) => {
        entries.forEach((e) => {
          if (e.isIntersecting) setActive(e.target.id);
        });
      },
      { rootMargin: '-28% 0px -62% 0px', threshold: 0 },
    );
    els.forEach((el) => obs.observe(el));
    return () => obs.disconnect();
  }, []);

  return (
    <div className={`container ${styles.layout}`}>
      <aside className={styles.sidebar}>
        <div className={styles.sidebarInner}>
          <button
            type="button"
            className={styles.navToggle}
            onClick={() => setNavOpen((o) => !o)}
            aria-expanded={navOpen}
          >
            Contents
            <span className={navOpen ? styles.chevOpen : styles.chev}>▾</span>
          </button>
          <nav className={`${styles.navList} ${navOpen ? styles.navListOpen : ''}`}>
            {NAV.map((g) => (
              <div className={styles.navGroup} key={g.group}>
                <p className={styles.navGroupTitle}>{g.group}</p>
                {g.items.map((it) => (
                  <a
                    key={it.id}
                    href={`#${it.id}`}
                    className={`${styles.navLink} ${active === it.id ? styles.navActive : ''}`}
                    onClick={() => setNavOpen(false)}
                  >
                    {it.label}
                  </a>
                ))}
              </div>
            ))}
          </nav>
        </div>
      </aside>

      <article className={styles.content}>
        <header className={styles.docHeader}>
          <p className="eyebrow">Documentation · v{SITE.version}</p>
          <h1 className={styles.docH1}>DuckRun</h1>
          <p className={styles.docLede}>
            An attribute-driven background job scheduler for .NET. Tag a method, give it a cron
            expression, and it runs inside the app you already deploy — with a real cancellation
            token, a captured console, and an optional control plane. This is the technical manual.
          </p>
          <p className={styles.preWarn}>
            <strong>Pre-1.0.</strong> DuckRun is alpha software. Names and signatures on this page can
            change between releases until 1.0.
          </p>
        </header>

        <div className={styles.prose}>
          {/* ── Introduction ─────────────────────────────────── */}
          <section id="introduction" className={styles.sec}>
            <p className={styles.kicker}>Getting started</p>
            <h2>Introduction</h2>
            <p>
              DuckRun schedules background work from an attribute. You write an ordinary method, put{' '}
              <code className="icode">[DuckRunJob(name, cron)]</code> on it, and a hosted service runs
              it on schedule using <strong>Cronos</strong> against <code className="icode">TimeZoneInfo.Local</code>.
              Each run gets a fresh DI scope, a cancellation token, and a console you can read live.
            </p>
            <p>It ships as four packages — add only what you need:</p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Package</th>
                  <th>Target frameworks</th>
                  <th>What it adds</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td><code className="icode">DuckRun.Core</code></td>
                  <td>net6.0 → net10.0</td>
                  <td>The scheduler, in-memory stores, embedded dashboard</td>
                </tr>
                <tr>
                  <td><code className="icode">DuckRun.EfCore</code></td>
                  <td>net10.0</td>
                  <td>Durable run history + console logs over EF Core</td>
                </tr>
                <tr>
                  <td><code className="icode">DuckRun.Redis</code></td>
                  <td>net48 + net10.0</td>
                  <td>Leader election + cluster-wide concurrency</td>
                </tr>
                <tr>
                  <td><code className="icode">DuckRun.Framework</code></td>
                  <td>net48</td>
                  <td>Classic ASP.NET host for the same job API</td>
                </tr>
              </tbody>
            </table>
            <p>
              The design goal is a flat on-ramp: <code className="icode">AddDuckRun</code> with nothing
              else gives you a working scheduler backed by in-memory ring buffers. Persistence and
              clustering are one line each, added later, with no change to your job code.
            </p>
          </section>

          {/* ── Installation ─────────────────────────────────── */}
          <section id="installation" className={styles.sec}>
            <p className={styles.kicker}>Getting started</p>
            <h2>Installation</h2>
            <p>Add the core package, plus any optional modules:</p>
            <CodeBlock
              lang="bash"
              label="terminal"
              code={`dotnet add package DuckRun.Core

# optional
dotnet add package DuckRun.EfCore     # durable history
dotnet add package DuckRun.Redis      # clustering`}
            />
            <p>
              Register DuckRun in <code className="icode">Program.cs</code>. Every{' '}
              <code className="icode">Use…</code> call below is optional — strip it back to just{' '}
              <code className="icode">AddJobsFromAssemblyContaining</code> to run fully in memory:
            </p>
            <CodeBlock lang="csharp" label="Program.cs" code={INSTALL_PROGRAM} />
            <p className={styles.note}>
              <code className="icode">MapDuckRunDashboard()</code> is only required when you call{' '}
              <code className="icode">UseStandaloneDashboard()</code>. The gRPC control-plane reporting
              from <code className="icode">UseDashboard(dsn)</code> needs no endpoint mapping.
            </p>
          </section>

          {/* ── Defining jobs ────────────────────────────────── */}
          <section id="jobs" className={styles.sec}>
            <p className={styles.kicker}>Authoring jobs</p>
            <h2>Defining jobs</h2>
            <p>
              A job is any method decorated with <code className="icode">[DuckRunJob]</code>. The
              declaring type is resolved from DI per run, so constructor injection works as usual:
            </p>
            <CodeBlock lang="csharp" label="ImportJobs.cs" code={JOB_EXAMPLE} />
            <p>
              The method may declare a <code className="icode">CancellationToken</code> and an{' '}
              <code className="icode">IDuckRunConsole</code> parameter — both are supplied
              automatically. The attribute carries the schedule and the run policy:
            </p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Parameter</th>
                  <th>Type</th>
                  <th>Default</th>
                  <th>Meaning</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td><code className="icode">name</code></td>
                  <td>string</td>
                  <td>—</td>
                  <td>Unique job name (required). Duplicates fail at startup.</td>
                </tr>
                <tr>
                  <td><code className="icode">cron</code></td>
                  <td>string</td>
                  <td>—</td>
                  <td>Cronos expression (required). 5- or 6-field; local time.</td>
                </tr>
                <tr>
                  <td><code className="icode">MaxConcurrency</code></td>
                  <td>int</td>
                  <td>1</td>
                  <td>In-flight runs allowed (cluster-wide with Redis).</td>
                </tr>
                <tr>
                  <td><code className="icode">TimeoutSeconds</code></td>
                  <td>int</td>
                  <td>0</td>
                  <td>Hard timeout. 0 disables it.</td>
                </tr>
                <tr>
                  <td><code className="icode">AllowManualTrigger</code></td>
                  <td>bool</td>
                  <td>true</td>
                  <td>Whether the run can be started by hand.</td>
                </tr>
                <tr>
                  <td><code className="icode">Enabled</code></td>
                  <td>bool</td>
                  <td>true</td>
                  <td>Set false to register a job without cron firing.</td>
                </tr>
              </tbody>
            </table>
            <p>
              Discovery is reflection-based at startup —{' '}
              <code className="icode">AddJobsFromAssembly(assembly)</code>,{' '}
              <code className="icode">AddJobsFromAssemblyContaining&lt;T&gt;()</code>, or a hand-built{' '}
              <code className="icode">AddJob(descriptor)</code>.
            </p>
          </section>

          {/* ── The console ──────────────────────────────────── */}
          <section id="console" className={styles.sec}>
            <p className={styles.kicker}>Authoring jobs</p>
            <h2>The console</h2>
            <p>
              <code className="icode">IDuckRunConsole</code> is scoped to the run. Anything you write is
              captured against that run and replayed in the dashboard — live while it executes, and
              from storage afterward if you persist it.
            </p>
            <CodeBlock lang="csharp" label="Rollup.cs" code={CONSOLE_EXAMPLE} />
            <p>
              The interface is small — <code className="icode">Info</code>,{' '}
              <code className="icode">Warning</code>, <code className="icode">Error</code>, and a{' '}
              <code className="icode">Log(level, message)</code> overload. When you are deep in a call
              stack where threading the console through would be noise, reach the ambient instance via{' '}
              <code className="icode">DuckRunConsole.Current</code> — an{' '}
              <code className="icode">AsyncLocal</code> accessor set for the duration of the run.
            </p>
            <p className={styles.note}>
              High-rate logging is fine. Writes go through a bounded ring buffer (tune with{' '}
              <code className="icode">ConsoleBufferSize</code>), and the EF Core store batches them off
              a channel so a chatty job never blocks on I/O.
            </p>
          </section>

          {/* ── Cancellation ─────────────────────────────────── */}
          <section id="cancellation" className={styles.sec}>
            <p className={styles.kicker}>Authoring jobs</p>
            <h2>Cancellation &amp; timeouts</h2>
            <p>
              The <code className="icode">CancellationToken</code> handed to a job is tripped by three
              things: the <code className="icode">TimeoutSeconds</code> elapsing, the host shutting
              down, or a cancel issued from the dashboard or{' '}
              <code className="icode">IDuckRunController</code>. Honour it — pass it to every{' '}
              <code className="icode">await</code> — and a job stops promptly and cleanly.
            </p>
            <p>
              DuckRun keeps the outcomes distinct rather than collapsing everything into one error
              bucket. A run lands in exactly one terminal state:
            </p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>JobRunState</th>
                  <th>Meaning</th>
                </tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">Pending</code></td><td>Queued, not yet started.</td></tr>
                <tr><td><code className="icode">Running</code></td><td>In progress.</td></tr>
                <tr><td><code className="icode">Succeeded</code></td><td>Returned without throwing.</td></tr>
                <tr><td><code className="icode">Failed</code></td><td>Threw — message and stack captured.</td></tr>
                <tr><td><code className="icode">Cancelled</code></td><td>Token tripped by shutdown or a manual cancel.</td></tr>
                <tr><td><code className="icode">TimedOut</code></td><td>Exceeded <code className="icode">TimeoutSeconds</code>.</td></tr>
              </tbody>
            </table>
            <p>
              The timeout is a linked token, not a thread abort — there is no{' '}
              <code className="icode">Thread.Abort</code> anywhere. Cooperative cancellation is the only
              mechanism, which is why threading the token through matters.
            </p>
          </section>

          {/* ── Manual triggers ──────────────────────────────── */}
          <section id="triggers" className={styles.sec}>
            <p className={styles.kicker}>Authoring jobs</p>
            <h2>Manual triggers</h2>
            <p>
              Inject <code className="icode">IDuckRunController</code> anywhere to drive jobs from code —
              an admin endpoint, a webhook, a test:
            </p>
            <CodeBlock lang="csharp" label="AdminEndpoints.cs" code={TRIGGER_EXAMPLE} />
            <p>The controller is the same surface the dashboard uses:</p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Returns</th>
                </tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">ListJobs()</code> / <code className="icode">GetJob(name)</code></td><td><code className="icode">JobDescriptor</code></td></tr>
                <tr><td><code className="icode">TriggerAsync(name, ct)</code></td><td><code className="icode">Guid</code> run id</td></tr>
                <tr><td><code className="icode">CancelAsync(runId, ct)</code></td><td><code className="icode">Task</code></td></tr>
                <tr><td><code className="icode">GetRecentRunsAsync(name, take, ct)</code></td><td><code className="icode">IReadOnlyList&lt;JobRun&gt;</code></td></tr>
                <tr><td><code className="icode">GetRunAsync(runId, ct)</code></td><td><code className="icode">JobRun?</code></td></tr>
                <tr><td><code className="icode">GetConsoleAsync(runId, ct)</code></td><td><code className="icode">IReadOnlyList&lt;ConsoleLogEntry&gt;</code></td></tr>
              </tbody>
            </table>
            <p className={styles.note}>
              A job with <code className="icode">AllowManualTrigger = false</code> rejects{' '}
              <code className="icode">TriggerAsync</code> and hides the button in the UI — useful for
              jobs that must only run on their schedule.
            </p>
          </section>

          {/* ── Embedded dashboard ───────────────────────────── */}
          <section id="standalone" className={styles.sec}>
            <p className={styles.kicker}>Dashboards</p>
            <h2>Embedded /duckrun</h2>
            <p>
              For a single instance, Core ships a self-contained mini-dashboard as embedded resources —
              no separate service, no build step. Turn it on and map it:
            </p>
            <CodeBlock
              lang="csharp"
              label="Program.cs"
              code={`o.UseStandaloneDashboard("/duckrun");
// …
app.MapDuckRunDashboard();`}
            />
            <p>
              Browse to <code className="icode">/duckrun</code> for the job list with next-run times,
              run history, and a live console. It is backed by a small JSON API under the same prefix —{' '}
              <code className="icode">GET /duckrun/api/jobs</code>,{' '}
              <code className="icode">GET /duckrun/api/runs/{'{id}'}/console</code>,{' '}
              <code className="icode">POST /duckrun/api/jobs/{'{name}'}/trigger</code>, and so on.
            </p>
            <p className={styles.note}>
              The embedded UI is per-instance. To watch many instances — or many projects — at once,
              use the central control plane below.
            </p>
          </section>

          {/* ── Control plane ────────────────────────────────── */}
          <section id="dashboard" className={styles.sec}>
            <p className={styles.kicker}>Dashboards</p>
            <h2>Control Server &amp; DSN</h2>
            <p>
              The Control Server is a separate container that aggregates every instrumented app. The
              model mirrors Sentry — <strong>projects</strong> grouped by <strong>teams</strong> and
              tenants — but tracks scheduled jobs instead of exceptions. Each app reports to it over
              gRPC, addressed by a <strong>DSN</strong>:
            </p>
            <CodeBlock
              lang="text"
              label="DSN format"
              code={`https://{publicKey}@{host}[:{port}]/{projectId}

https://pk_live_8f3a@dashboard.example.com/42`}
            />
            <p>
              Wire it up with <code className="icode">UseDashboard(dsn)</code>. On startup the runtime
              handshakes, then a background loop ships runs, console logs and heartbeats — batched, and
              authenticated by an <code className="icode">x-duckrun-key</code> gRPC header. The protocol
              is versioned by its proto package (<code className="icode">duckrun.protocol.v1</code>),
              with services <code className="icode">Handshake</code>,{' '}
              <code className="icode">SendHeartbeat</code>, <code className="icode">SendRuns</code> and{' '}
              <code className="icode">SendLogs</code>.
            </p>
            <h3>Running the dashboard</h3>
            <p>
              It is one image — ASP.NET Core backend, the React SPA, and a pluggable EF Core database.
              Configure it through environment variables:
            </p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Variable</th>
                  <th>Purpose</th>
                </tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">DuckRun__DashboardSecret</code></td><td>Signs DSN keys + cookies. Rotating it invalidates all DSNs.</td></tr>
                <tr><td><code className="icode">DuckRun__Db__Provider</code></td><td><code className="icode">Postgres</code>, <code className="icode">SqlServer</code>, or <code className="icode">MySql</code>.</td></tr>
                <tr><td><code className="icode">DuckRun__Db__ConnectionString</code></td><td>ADO.NET connection string.</td></tr>
                <tr><td><code className="icode">DuckRun__Auth__InitialAdmin…</code></td><td>First-boot local admin (email + password).</td></tr>
                <tr><td><code className="icode">DuckRun__PublicBaseUrl</code></td><td>External URL used when issuing DSNs.</td></tr>
                <tr><td><code className="icode">DuckRun__Retention__RunDays</code></td><td>Run-record retention (default 30).</td></tr>
              </tbody>
            </table>
            <CodeBlock lang="yaml" label="docker-compose.yml" code={COMPOSE} />
            <p className={styles.note}>
              The control plane is optional. The runtime is fully functional without it — losing the
              dashboard never stops a job from running.
            </p>
          </section>

          {/* ── Persistence ──────────────────────────────────── */}
          <section id="persistence" className={styles.sec}>
            <p className={styles.kicker}>Scaling out</p>
            <h2>Persistence</h2>
            <p>
              By default, run history and console output live in bounded in-memory buffers — great for
              a single box, gone on restart. Add <code className="icode">DuckRun.EfCore</code> to make
              them durable:
            </p>
            <CodeBlock
              lang="csharp"
              label="Program.cs"
              code={`o.UseEfCore(builder.Configuration.GetConnectionString("DuckRun"), DuckRunProvider.Postgres);`}
            />
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>DuckRunProvider</th>
                  <th>Notes</th>
                </tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">SqlServer</code></td><td>SQL Server / Azure SQL.</td></tr>
                <tr><td><code className="icode">Postgres</code></td><td>PostgreSQL via Npgsql.</td></tr>
                <tr><td><code className="icode">CockroachDb</code></td><td>CockroachDB, wire-compatible via Npgsql.</td></tr>
              </tbody>
            </table>
            <p>
              There is <strong>no migrations step</strong>. On first boot the schema is created
              idempotently with <code className="icode">EnsureCreatedAsync</code> into a{' '}
              <code className="icode">DuckRun</code> schema. Timestamps are stored as UTC{' '}
              <code className="icode">DateTime</code> (not <code className="icode">DateTimeOffset</code>)
              so ordering is stable across providers, and console writes are batched off a channel.
            </p>
          </section>

          {/* ── Clustering ───────────────────────────────────── */}
          <section id="cluster" className={styles.sec}>
            <p className={styles.kicker}>Scaling out</p>
            <h2>Clustering</h2>
            <p>
              Run more than one instance and you need exactly one of them driving the schedule.{' '}
              <code className="icode">DuckRun.Redis</code> provides that through leader election:
            </p>
            <CodeBlock
              lang="csharp"
              label="Program.cs"
              code={`o.UseRedis("redis-1:6379,redis-2:6379,redis-3:6379");`}
            />
            <p>
              One instance wins a Lua-scripted lock with a TTL lease and runs the tick loop; the others
              wait. The leader refreshes its lease on an interval; if it dies, a follower is promoted
              within seconds and resets the next-run schedule. <code className="icode">MaxConcurrency</code>{' '}
              becomes cluster-wide via sorted-set slots with a TTL safety net.
            </p>
            <p>
              Keys are namespaced{' '}
              <code className="icode">duckrun:{'{projectId}'}:{'{environment}'}:…</code> so the same
              project across dev, staging and prod — or several projects — can share one Redis without
              colliding. <code className="icode">environment</code> defaults from{' '}
              <code className="icode">ASPNETCORE_ENVIRONMENT</code> /{' '}
              <code className="icode">DOTNET_ENVIRONMENT</code>, falling back to{' '}
              <code className="icode">"Production"</code>.
            </p>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Option</th>
                  <th>Default</th>
                </tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">LeaderLeaseDuration</code></td><td>15s</td></tr>
                <tr><td><code className="icode">LeaderRefreshInterval</code></td><td>5s</td></tr>
                <tr><td><code className="icode">HeartbeatTtl</code></td><td>30s</td></tr>
              </tbody>
            </table>
            <p className={styles.note}>
              Clustering is abstracted behind <code className="icode">IClusterCoordinator</code>. Without
              the Redis package, the default <code className="icode">LocalClusterCoordinator</code> is a
              single-node always-leader — the same code path, no Redis required.
            </p>
          </section>

          {/* ── .NET Framework ───────────────────────────────── */}
          <section id="framework" className={styles.sec}>
            <p className={styles.kicker}>Platforms</p>
            <h2>.NET Framework 4.8</h2>
            <p>
              <code className="icode">DuckRun.Framework</code> brings the same job model to classic
              ASP.NET. The <code className="icode">[DuckRunJob]</code> source is identical — the net48
              build links 26 files straight from Core rather than forking them — so a job written once
              compiles on both runtimes. You start it from <code className="icode">Global.asax</code>:
            </p>
            <CodeBlock lang="csharp" label="Global.asax.cs" code={GLOBAL_ASAX} />
            <p>What differs from modern .NET:</p>
            <ul>
              <li>
                There is no built-in DI container, so jobs with constructor dependencies need a{' '}
                <code className="icode">UseJobFactory(Func&lt;Type, object&gt;)</code> bridge to yours
                (Autofac, Windsor, MVC's resolver…). Without one, jobs are created via{' '}
                <code className="icode">Activator.CreateInstance</code>.
              </li>
              <li>
                <code className="icode">CancellationToken</code> and{' '}
                <code className="icode">IDuckRunConsole</code> are still injected into the method
                signature.
              </li>
              <li>
                <code className="icode">DuckRun.Redis</code> works (it multi-targets net48), so net48
                instances cluster too.
              </li>
              <li>
                The embedded <code className="icode">/duckrun</code> UI and gRPC reporting are not yet on
                the net48 build — they are deferred follow-ups.
              </li>
            </ul>
          </section>

          {/* ── API reference ────────────────────────────────── */}
          <section id="reference" className={styles.sec}>
            <p className={styles.kicker}>Reference</p>
            <h2>API surface</h2>
            <h3>Registration</h3>
            <table className={styles.table}>
              <thead>
                <tr><th>Member</th><th>Where</th></tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">AddDuckRun(Action&lt;DuckRunOptionsBuilder&gt;)</code></td><td><code className="icode">IServiceCollection</code></td></tr>
                <tr><td><code className="icode">MapDuckRunDashboard(pathPrefix?)</code></td><td><code className="icode">IEndpointRouteBuilder</code></td></tr>
                <tr><td><code className="icode">AddJobsFromAssembly</code> / <code className="icode">…Containing&lt;T&gt;</code> / <code className="icode">AddJob</code></td><td>builder</td></tr>
                <tr><td><code className="icode">UseStandaloneDashboard(path)</code> · <code className="icode">UseDashboard(dsn)</code></td><td>builder</td></tr>
                <tr><td><code className="icode">UseEfCore(conn, provider)</code> · <code className="icode">UseRedis(conn, …)</code></td><td>builder (modules)</td></tr>
                <tr><td><code className="icode">ConsoleBufferSize(n)</code> · <code className="icode">RunHistorySize(n)</code></td><td>builder</td></tr>
              </tbody>
            </table>
            <h3>Authoring &amp; control</h3>
            <table className={styles.table}>
              <thead>
                <tr><th>Type</th><th>Role</th></tr>
              </thead>
              <tbody>
                <tr><td><code className="icode">DuckRunJobAttribute</code></td><td>Marks + configures a job method.</td></tr>
                <tr><td><code className="icode">IDuckRunConsole</code> · <code className="icode">DuckRunConsole.Current</code></td><td>Per-run logging (scoped + ambient).</td></tr>
                <tr><td><code className="icode">IDuckRunController</code></td><td>Trigger / cancel / query at runtime.</td></tr>
                <tr><td><code className="icode">JobDescriptor</code></td><td>Registered-job metadata.</td></tr>
                <tr><td><code className="icode">JobRun</code> · <code className="icode">JobRunState</code></td><td>A single invocation + its terminal state.</td></tr>
                <tr><td><code className="icode">ConsoleLogEntry</code> · <code className="icode">DuckRunLogLevel</code></td><td>One captured console line.</td></tr>
                <tr><td><code className="icode">IClusterCoordinator</code> · <code className="icode">NodeInfo</code></td><td>Leadership + cluster topology.</td></tr>
              </tbody>
            </table>
          </section>

          {/* ── FAQ ──────────────────────────────────────────── */}
          <section id="faq" className={styles.sec}>
            <p className={styles.kicker}>Reference</p>
            <h2>FAQ</h2>

            <h3>Is it production-ready?</h3>
            <p>
              Not yet — it is <code className="icode">{SITE.version}</code>. The boring, load-bearing
              parts (cancellation, console capture, leader election) are the focus, but the API will
              move before 1.0. Pin a version and read the release notes.
            </p>

            <h3>Does it need a database?</h3>
            <p>
              No. With just <code className="icode">DuckRun.Core</code> everything is in memory. Add{' '}
              <code className="icode">DuckRun.EfCore</code> only when you want history to survive a
              restart.
            </p>

            <h3>How is this different from Hangfire, Quartz or TickerQ?</h3>
            <p>
              DuckRun starts with zero infrastructure, treats fail / cancel / timeout as distinct
              outcomes, captures a console per run, and compiles the same job source on .NET 10 and
              .NET Framework 4.8. It is younger and smaller than all three — that is the trade.
            </p>

            <h3>Can I schedule down to the second?</h3>
            <p>
              Yes — Cronos accepts 6-field expressions, so{' '}
              <code className="icode">*/15 * * * * *</code> is every fifteen seconds. Schedules use{' '}
              <code className="icode">TimeZoneInfo.Local</code>.
            </p>

            <h3>What if I run multiple instances without Redis?</h3>
            <p>
              Each becomes its own leader and every instance fires the schedule — you get duplicate
              runs. Add <code className="icode">DuckRun.Redis</code> so exactly one instance drives the
              tick.
            </p>

            <h3>Licence?</h3>
            <p>
              {SITE.license}. Source and issues are on{' '}
              <a href={SITE.github} target="_blank" rel="noreferrer" className={styles.inlineLink}>GitHub ↗</a>.
            </p>
          </section>
        </div>
      </article>
    </div>
  );
}
