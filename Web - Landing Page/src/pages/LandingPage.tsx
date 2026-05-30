import { Link } from 'react-router-dom';
import CodeBlock from '../components/CodeBlock';
import DuckScene from '../components/DuckScene';
import { SITE, PACKAGES } from '../site';
import styles from './LandingPage.module.css';

const STATS = [
  { n: '1', l: 'attribute to learn', s: '[DuckRunJob]' },
  { n: '5', l: 'runtimes, one source', s: 'net6 → net10' },
  { n: '4', l: 'durable stores', s: 'SQL · PG · Cockroach · SQLite' },
  { n: '26', l: 'files linked into net48', s: 'same job API' },
  { n: '0', l: 'migrations to run', s: 'schema on boot' },
];

const PROGRAM_CS = `public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDuckRun(o =>
        {
            o.AddJobsFromAssemblyContaining<Program>();
            o.UseStandaloneDashboard("/duckrun");
        });

        var app = builder.Build();
        app.MapDuckRunDashboard();

        await app.RunAsync();
    }
}`;

const JOB_CS = `public sealed class ReportingJobs(IReportService reports)
{
    [DuckRunJob("daily-revenue", "0 2 * * *", MaxConcurrency = 1, TimeoutSeconds = 3600)]
    public async Task Run(IDuckRunConsole console, CancellationToken ct)
    {
        console.Info("Fetching the last 24h of transactions…");
        var rows = await reports.FetchAsync(ct);

        console.Info($"Aggregating {rows.Count} rows");
        await reports.AggregateAsync(rows, ct);
    }
}`;

export default function LandingPage() {
  return (
    <div className={styles.page}>
      {/* ── Hero ─────────────────────────────────────────────── */}
      <section className={styles.hero} id="hero">
        <div className={`container ${styles.heroGrid}`}>
          <div className={styles.heroText}>
            <p className="eyebrow">
              DuckRun · v{SITE.version} · {SITE.license}
            </p>
            <h1 className={styles.h1}>
              Just another .NET
              <br />
              job scheduler. <span className={styles.accent}>Allegedly.</span>
            </h1>
            <p className={styles.lede}>
              Attribute-driven background jobs with a real <code className="icode">CancellationToken</code>,
              a live console per run, Redis leader election, and an optional control plane that reads
              every instance you run. .NET Framework 4.8 through .NET 10.
            </p>
            <div className={styles.heroCtas}>
              <Link to="/docs#installation" className="btn btn--primary">
                Read the docs
              </Link>
              <a href={SITE.github} target="_blank" rel="noreferrer" className="btn btn--ghost">
                GitHub ↗
              </a>
            </div>
            <div className={styles.heroTags}>
              <span className="tag">net48 → net10</span>
              <span className="tag">Cronos cron</span>
              <span className="tag">EF Core 10</span>
              <span className="tag">gRPC ingest</span>
            </div>
          </div>
          <div className={styles.heroScene}>
            <DuckScene />
            <p className={styles.sceneCaption}>
              <span className="statusDot" /> click a node — watch leadership fail over
            </p>
          </div>
        </div>
      </section>

      {/* ── Building-blocks strip (no heading — breaks the pattern) ─ */}
      <div className={styles.strip}>
        <div className={`container ${styles.stripRow}`}>
          <span>.NET 6 · 7 · 8 · 9 · 10</span>
          <span className={styles.stripDot}>/</span>
          <span>.NET Framework 4.8</span>
          <span className={styles.stripDot}>/</span>
          <span>Cronos</span>
          <span className={styles.stripDot}>/</span>
          <span>StackExchange.Redis</span>
          <span className={styles.stripDot}>/</span>
          <span>MessagePack</span>
          <span className={styles.stripDot}>/</span>
          <span>SignalR</span>
        </div>
      </div>

      {/* ── Quickstart ───────────────────────────────────────── */}
      <section className={styles.section} id="quickstart">
        <div className="container">
          <div className={styles.sectionHead}>
            <p className="eyebrow">Quickstart</p>
            <h2 className={styles.h2}>Three steps to your first run.</h2>
            <p className={styles.sub}>
              No host to stand up, no broker to babysit. The scheduler lives inside the app you
              already deploy.
            </p>
          </div>

          <div className={styles.steps}>
            <div className={styles.step}>
              <div className={styles.stepHead}>
                <span className={styles.stepNo}>01</span>
                <h3 className={styles.stepTitle}>Install the package</h3>
              </div>
              <CodeBlock lang="bash" label="terminal" code={`dotnet add package DuckRun.Core`} />
            </div>

            <div className={styles.step}>
              <div className={styles.stepHead}>
                <span className={styles.stepNo}>02</span>
                <h3 className={styles.stepTitle}>Register it</h3>
              </div>
              <CodeBlock lang="csharp" label="Program.cs" code={PROGRAM_CS} />
            </div>

            <div className={styles.step}>
              <div className={styles.stepHead}>
                <span className={styles.stepNo}>03</span>
                <h3 className={styles.stepTitle}>Tag a method</h3>
              </div>
              <CodeBlock lang="csharp" label="ReportingJobs.cs" code={JOB_CS} />
            </div>
          </div>

          <p className={styles.stepFoot}>
            That is the whole loop. Browse <code className="icode">/duckrun</code> to watch it tick,
            cancel a run mid-flight, or trigger one by hand.
          </p>
        </div>
      </section>

      {/* ── Stat strip ───────────────────────────────────────── */}
      <section className={styles.statsWrap}>
        <div className={`container ${styles.stats}`}>
          {STATS.map((s) => (
            <div className={styles.stat} key={s.l}>
              <div className={styles.statN}>{s.n}</div>
              <div className={styles.statL}>{s.l}</div>
              <div className={styles.statS}>{s.s}</div>
            </div>
          ))}
        </div>
      </section>

      {/* ── Features (asymmetric grid) ───────────────────────── */}
      <section className={styles.section} id="features">
        <div className="container">
          <div className={styles.sectionHead}>
            <p className="eyebrow">What you get</p>
            <h2 className={styles.h2}>The parts most schedulers leave to you.</h2>
          </div>

          <div className={styles.fgrid}>
            <article className={styles.lead}>
              <span className={styles.cardIdx}>F-01</span>
              <h3 className={styles.cardTitle}>Cancellation that actually cancels.</h3>
              <p className={styles.cardBody}>
                Every run gets a real <code className="icode">CancellationToken</code> — tripped by a
                timeout, by host shutdown, or by a click in the dashboard. DuckRun keeps the three
                apart: a run that <em>failed</em>, a run you <em>cancelled</em>, and a run that{' '}
                <em>timed out</em> are three different states, not one beige “errored”. The exception
                and its stack are captured either way.
              </p>
              <ul className={styles.cardList}>
                <li>Per-run DI scope, disposed when the run ends</li>
                <li>
                  <code className="icode">TimeoutSeconds</code> turns into a linked token, not a thread
                  abort
                </li>
                <li>Cancel propagates to every <code className="icode">await</code> you pass it</li>
              </ul>
            </article>

            <article className={styles.card}>
              <span className={styles.cardIdx}>F-02</span>
              <h3 className={styles.cardTitle}>A console per run.</h3>
              <p className={styles.cardBody}>
                Inject <code className="icode">IDuckRunConsole</code> and write{' '}
                <code className="icode">Info</code> / <code className="icode">Warning</code> /{' '}
                <code className="icode">Error</code>. Every line is captured, buffered, and replayed
                in the UI — live while it runs, and months later if you persist it.
              </p>
            </article>

            <article className={styles.card}>
              <span className={styles.cardIdx}>F-03</span>
              <h3 className={styles.cardTitle}>Discovery by attribute.</h3>
              <p className={styles.cardBody}>
                Tag a method with <code className="icode">[DuckRunJob(name, cron)]</code>. Reflection
                finds it on boot. Duplicate names fail loudly at startup, not silently at 2am.
              </p>
            </article>

            <article className={styles.card}>
              <span className={styles.cardIdx}>F-04</span>
              <h3 className={styles.cardTitle}>Cluster-wide concurrency.</h3>
              <p className={styles.cardBody}>
                <code className="icode">MaxConcurrency = 3</code> means three in flight across the
                whole cluster — not three per process. Redis sorted-set slots with a TTL safety net,
                no thundering herd.
              </p>
            </article>

            <article className={styles.card}>
              <span className={styles.cardIdx}>F-05</span>
              <h3 className={styles.cardTitle}>Bring your own database. Or none.</h3>
              <p className={styles.cardBody}>
                In-memory ring buffers by default — nothing to provision. Swap in EF Core for durable
                run history with one line and exactly zero migrations.
              </p>
            </article>
          </div>
        </div>
      </section>

      {/* ── Why (essay, drop cap — breaks the pattern) ───────── */}
      <section className={styles.whyWrap} id="why">
        <div className="container">
          <div className={styles.why}>
            <p className={styles.whyKicker}>Why build another one</p>
            <div className={styles.whyBody}>
              <p>
                Hangfire is the one everyone reaches for, and I kept reaching past it — its DI model
                and the way it persists jobs were a constant low-grade fight. TickerQ is a clean,
                modern take, but there is no central control plane, no master server to watch every
                instance at once, and standing up a cluster gets complicated fast. Quartz does the job
                and is quirky in all its own ways. They are good libraries. None of them was the one on
                my list.
              </p>
              <p>
                And the list was specific: one attribute to start, a real cancellation token, a console
                I can read per run, DI that just works, durable history when I want it — and a single
                dashboard that sees every instance and every project, without the clustering turning
                into its own side project. Nothing I picked up did the whole list without a fight.
              </p>
              <p>
                So DuckRun starts at zero. <code className="icode">AddDuckRun</code>, one method,
                everything in memory. Persistence and clustering are a line each when you outgrow it,
                and the job never changes — the same <code className="icode">[DuckRunJob]</code> compiles
                on .NET 10 and .NET Framework 4.8 from the exact same 26 linked files. It is pre-1.0 and
                the API will move. But the boring parts — the DI, the persistence, leader election, a
                control plane that actually sees everything — are the parts I wanted to get right first.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── Packages ─────────────────────────────────────────── */}
      <section className={styles.section} id="packages">
        <div className="container">
          <div className={styles.sectionHead}>
            <p className="eyebrow">On NuGet</p>
            <h2 className={styles.h2}>Four packages. Take what you need.</h2>
            <p className={styles.sub}>
              Core does the scheduling. The other three are opt-in — add a database, add a cluster, or
              drop the whole thing into classic ASP.NET.
            </p>
          </div>

          <div className={styles.pkgs}>
            {PACKAGES.map((p, i) => (
              <a
                key={p.id}
                href={SITE.nuget(p.id)}
                target="_blank"
                rel="noreferrer"
                className={styles.pkg}
              >
                <span className={styles.pkgIdx}>{String(i + 1).padStart(2, '0')}</span>
                <div className={styles.pkgMain}>
                  <h3 className={styles.pkgName}>{p.id}</h3>
                  <p className={styles.pkgBlurb}>{p.blurb}</p>
                </div>
                <div className={styles.pkgSide}>
                  <span className="tag">{p.tfm}</span>
                  <span className={styles.pkgGo}>nuget ↗</span>
                </div>
              </a>
            ))}
          </div>
        </div>
      </section>

      {/* ── Architecture rail ────────────────────────────────── */}
      <section className={styles.section} id="architecture">
        <div className="container">
          <div className={styles.sectionHead}>
            <p className="eyebrow">How it fits together</p>
            <h2 className={styles.h2}>Three moving parts. Two are optional.</h2>
          </div>

          <div className={styles.rail}>
            <div className={styles.railStep}>
              <span className={styles.railNo}>01</span>
              <h3 className={styles.railTitle}>Your app</h3>
              <p className={styles.railBody}>
                The scheduler runs in-process as a hosted service. Cronos drives the tick, jobs run in
                scoped DI. This works completely on its own — no network required.
              </p>
            </div>
            <div className={styles.railConn} aria-hidden="true" />
            <div className={styles.railStep}>
              <span className={styles.railNo}>02</span>
              <h3 className={styles.railTitle}>The DSN</h3>
              <p className={styles.railBody}>
                Point at a dashboard with <code className="icode">UseDashboard(dsn)</code>. A gRPC
                client ships runs, console logs and heartbeats — batched, authed by key, versioned by
                proto package.
              </p>
            </div>
            <div className={styles.railConn} aria-hidden="true" />
            <div className={styles.railStep}>
              <span className={styles.railNo}>03</span>
              <h3 className={styles.railTitle}>The control plane</h3>
              <p className={styles.railBody}>
                One container aggregates every instance and every project. Multi-tenant, per-project
                DSNs, live console over SignalR, and a command channel to trigger runs by hand.
              </p>
            </div>
          </div>
          <p className={styles.railFoot}>
            No dashboard? The embedded <code className="icode">/duckrun</code> page still gives you
            jobs, run history and a live console for a single instance — shipped as resources inside
            Core, nothing to deploy.
          </p>
        </div>
      </section>

      {/* ── Persistence + Cluster (two-up, hairline split) ───── */}
      <section className={styles.section} id="infra">
        <div className="container">
          <div className={styles.twoUp}>
            <div className={styles.half}>
              <p className="eyebrow">Persistence · DuckRun.EfCore</p>
              <h3 className={styles.halfTitle}>Durable history, no migrations.</h3>
              <p className={styles.halfBody}>
                Job runs and console output, backed by EF Core. The schema is created on first boot
                with <code className="icode">EnsureCreated</code>; console writes are channel-buffered
                and flushed in batches so a chatty job never blocks.
              </p>
              <CodeBlock
                lang="csharp"
                label="Program.cs"
                code={`o.UseEfCore(connectionString, DuckRunProvider.Postgres);`}
              />
              <div className={styles.chips}>
                <span className="tag">SQL Server</span>
                <span className="tag">PostgreSQL</span>
                <span className="tag">CockroachDB</span>
                <span className="tag">SQLite</span>
              </div>
            </div>

            <div className={styles.half}>
              <p className="eyebrow">Clustering · DuckRun.Redis</p>
              <h3 className={styles.halfTitle}>One leader, picked in Lua.</h3>
              <p className={styles.halfBody}>
                Run as many instances as you like. One wins a TTL-leased lock and drives the tick loop;
                the rest wait. Failover takes seconds. Keys are namespaced{' '}
                <code className="icode">duckrun:{'{project}'}:{'{env}'}</code> so dev, staging and prod
                can share one Redis without colliding.
              </p>
              <CodeBlock
                lang="csharp"
                label="Program.cs"
                code={`o.UseRedis("redis-1:6379,redis-2:6379,redis-3:6379");`}
              />
              <div className={styles.chips}>
                <span className="tag">Lua election</span>
                <span className="tag">TTL lease</span>
                <span className="tag">heartbeats</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── Dashboard (dark band) ────────────────────────────── */}
      <section className={styles.dashWrap} id="dashboard">
        <div className={`container ${styles.dashGrid}`}>
          <div className={styles.dashText}>
            <p className="eyebrow" style={{ color: 'var(--text-muted-on-dark)' }}>
              The control plane
            </p>
            <h2 className={styles.dashH2}>A dashboard you can actually read.</h2>
            <p className={styles.dashBody}>
              One small container — backend, SPA and database in a single image. Each app you
              instrument becomes a project with its own DSN. You get live runs, per-run console output,
              cluster topology, and a button that triggers a job over the wire.
            </p>
            <div className={styles.dashCtas}>
              <Link to="/docs#dashboard" className="btn btn--onDark">
                Run the dashboard
              </Link>
              <a href={SITE.github} target="_blank" rel="noreferrer" className="btn btn--ghostDark">
                Compose &amp; k8s ↗
              </a>
            </div>
          </div>

          <div className={styles.dashCard}>
            <div className={styles.dashCardBar}>
              <span className="statusDot" />
              <span>dashboard · project 42 · 3 nodes</span>
            </div>
            <div className={styles.dsnRow}>
              <span className={styles.dsnLabel}>DSN</span>
              <code className={styles.dsn}>
                https://<span className={styles.dsnKey}>pk_live_8f3a</span>@dashboard.example.com/42
              </code>
            </div>
            <ul className={styles.logList}>
              <li>
                <span className={styles.logOk}>✓</span> daily-revenue · 1.2s · node-1
              </li>
              <li>
                <span className={styles.logRun}>•</span> import-feed · running · 412 ms
              </li>
              <li>
                <span className={styles.logWarn}>!</span> flaky-report · retry 2/3
              </li>
              <li>
                <span className={styles.logOk}>✓</span> cleanup-temp · 84 ms · node-2
              </li>
            </ul>
          </div>
        </div>
      </section>

      {/* ── Closing CTA ──────────────────────────────────────── */}
      <section className={styles.section} id="start">
        <div className="container">
          <div className={styles.cta}>
            <h2 className={styles.ctaH2}>Install it. Break it. Open an issue.</h2>
            <p className={styles.ctaSub}>
              It is alpha software with a waterfowl for a logo. That is exactly the right time to try
              it and tell me what is wrong.
            </p>
            <div className={styles.ctaCode}>
              <CodeBlock lang="bash" label="terminal" code={`dotnet add package DuckRun.Core`} />
            </div>
            <div className={styles.ctaBtns}>
              <Link to="/docs" className="btn btn--primary">
                Read the docs
              </Link>
              <a href={SITE.github} target="_blank" rel="noreferrer" className="btn btn--ghost">
                Star on GitHub ↗
              </a>
            </div>
            <p className={styles.ctaSign}>
              <span className="statusDot" />
              {SITE.author} — still pre-1.0, still listening.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}
