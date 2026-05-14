import { useGlobalKpis, useProjects } from '../api/hooks';
import { PageHeader, Section, KpiCard, EmptyState } from '../shell/ui';
import { fmtRelative } from '../lib/format';
import { Link } from 'react-router-dom';

export function GlobalOverviewPage() {
  const kpis = useGlobalKpis();
  const projects = useProjects();

  const runs24h = kpis.data?.runs24h ?? [];
  const succeeded = runs24h.find((r) => r.state === 'Succeeded')?.count ?? 0;
  const failed = runs24h.find((r) => r.state === 'Failed')?.count ?? 0;
  const cancelled = runs24h.find((r) => r.state === 'Cancelled')?.count ?? 0;
  const totalFinished = succeeded + failed + cancelled;
  const failureRate = totalFinished > 0 ? Math.round((failed / totalFinished) * 100) : 0;

  return (
    <>
      <PageHeader eyebrow="across every project" title="Overview" />

      <Section>
        <div className="grid grid-cols-2 md:grid-cols-4 border-t border-l hairline">
          <KpiCard label="projects" value={kpis.data?.totalProjects ?? '—'} />
          <KpiCard label="jobs registered" value={kpis.data?.totalJobs ?? '—'} />
          <KpiCard label="nodes alive" value={kpis.data?.activeNodes ?? '—'} hint="heartbeat in last 60s" />
          <KpiCard label="running now" value={kpis.data?.running ?? '—'} emphasised />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 border-l hairline mt-10">
          <KpiCard label="succeeded · 24h" value={succeeded} />
          <KpiCard label="failed · 24h" value={failed} />
          <KpiCard
            label="failure rate · 24h"
            value={`${failureRate}%`}
            hint={`${totalFinished} runs finished`}
          />
        </div>
      </Section>

      <Section>
        <div className="flex items-baseline justify-between mb-5">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 20, fontWeight: 500, letterSpacing: '-0.02em' }}>
            Projects
          </h2>
          <Link to="/projects" className="eyebrow hover:text-brand transition-colors">
            see all →
          </Link>
        </div>

        {projects.data && projects.data.length === 0 && (
          <EmptyState message="No projects yet. Create one to get a DSN." />
        )}

        {projects.data && projects.data.length > 0 && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 border-t border-l hairline">
            {projects.data.slice(0, 6).map((p) => (
              <Link
                key={p.id}
                to={`/projects/${p.id}`}
                className="px-6 py-5 border-r border-b hairline hover:bg-bg-hover/40 transition-colors"
              >
                <div className="eyebrow mb-1">project</div>
                <div className="font-medium" style={{ fontFamily: 'var(--font-heading)', fontSize: 17, letterSpacing: '-0.01em' }}>
                  {p.name}
                </div>
                <div className="text-text-muted text-[12px] mt-1">
                  created {fmtRelative(p.createdAt)}
                </div>
              </Link>
            ))}
          </div>
        )}
      </Section>
    </>
  );
}
