// Single source of truth for external links + product facts.
// NOTE: the GitHub/NuGet URLs are sensible placeholders — swap in the real ones.
export const SITE = {
  name: 'DuckRun',
  tagline: 'Just another .NET job scheduler.',
  version: '0.1.0-alpha',
  license: 'MIT',
  author: 'Kyle Müller',
  github: 'https://github.com/kylemueller/duckrun',
  issues: 'https://github.com/kylemueller/duckrun/issues',
  nugetSearch: 'https://www.nuget.org/packages?q=DuckRun',
  nuget: (id: string) => `https://www.nuget.org/packages/${id}`,
} as const;

// `extends` lists the base package(s) a module plugs into.
// An empty list marks an independent base (Core and Framework stand alone).
export const PACKAGES = [
  {
    id: 'DuckRun.Core',
    tfm: 'net6.0 → net10.0',
    extends: [] as readonly string[],
    blurb:
      'The scheduler. Attribute discovery, the Cronos tick loop, per-run DI scopes, cancellation, the in-memory stores, and the embedded /duckrun dashboard.',
  },
  {
    id: 'DuckRun.EfCore',
    tfm: 'net10.0',
    extends: ['Core'] as readonly string[],
    blurb:
      'Durable run history and console logs over Entity Framework Core. SQL Server, PostgreSQL, and CockroachDB. No migrations step — schema is bootstrapped on boot.',
  },
  {
    id: 'DuckRun.Redis',
    tfm: 'net48 + net10.0',
    extends: ['Core', 'Framework'] as readonly string[],
    blurb:
      'Multi-instance orchestration. Lua leader election with a TTL lease, cluster-wide concurrency slots, and per-node heartbeats. One package, two builds.',
  },
  {
    id: 'DuckRun.Framework',
    tfm: 'net48',
    extends: [] as readonly string[],
    blurb:
      'Classic ASP.NET on .NET Framework 4.8. Same [DuckRunJob] source — 26 files linked from Core — started from Global.asax with DuckRunHost.Start.',
  },
] as const;
