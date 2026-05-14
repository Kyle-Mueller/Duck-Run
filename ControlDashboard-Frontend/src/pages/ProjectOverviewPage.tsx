import { Link, useParams } from 'react-router-dom';
import { useProject, useProjectKpis, useProjectRuns } from '../api/hooks';
import { KpiCard, PageHeader, Section, StatePill, Table, Td, Th, EmptyState } from '../shell/ui';
import { fmtDuration, fmtRelative, shortId } from '../lib/format';

export function ProjectOverviewPage() {
  const { id = '' } = useParams();
  const project = useProject(id);
  const kpis = useProjectKpis(id);
  const runs = useProjectRuns(id, 8);

  const runs24h = kpis.data?.runs24h ?? [];
  const succeeded = runs24h.find((r) => r.state === 'Succeeded')?.count ?? 0;
  const failed = runs24h.find((r) => r.state === 'Failed')?.count ?? 0;

  return (
    <>
      <PageHeader eyebrow="project" title={project.data?.name ?? '—'} />

      <Section>
        <div className="grid grid-cols-2 md:grid-cols-4 border-t border-l hairline">
          <KpiCard label="jobs" value={kpis.data?.jobs ?? '—'} />
          <KpiCard
            label="nodes alive"
            value={`${kpis.data?.nodesAlive ?? '—'} / ${kpis.data?.nodesTotal ?? '—'}`}
            hint="heartbeat in last 60s"
          />
          <KpiCard
            label="leader"
            value={kpis.data?.leader ? <code style={{ fontSize: 18 }}>{shortId(kpis.data.leader)}</code> : '—'}
            hint={kpis.data?.leader ? 'this node fires crons' : 'no leader yet'}
          />
          <KpiCard label="running now" value={kpis.data?.running ?? '—'} emphasised />
        </div>

        <div className="grid grid-cols-2 md:grid-cols-3 border-l hairline mt-10">
          <KpiCard label="succeeded · 24h" value={succeeded} />
          <KpiCard label="failed · 24h" value={failed} />
          <KpiCard
            label="success rate · 24h"
            value={
              succeeded + failed > 0 ? `${Math.round((succeeded / (succeeded + failed)) * 100)}%` : '—'
            }
            hint={`${succeeded + failed} finished`}
          />
        </div>
      </Section>

      <Section>
        <div className="flex items-baseline justify-between mb-5">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 20, fontWeight: 500, letterSpacing: '-0.02em' }}>
            Recent runs
          </h2>
          <Link to={`/projects/${id}/runs`} className="eyebrow hover:text-brand transition-colors">
            see all →
          </Link>
        </div>

        {runs.data && runs.data.length === 0 && <EmptyState message="No runs yet." />}

        {runs.data && runs.data.length > 0 && (
          <Table>
            <thead>
              <tr>
                <Th>job</Th>
                <Th>state</Th>
                <Th>started</Th>
                <Th>duration</Th>
                <Th>trigger</Th>
                <Th></Th>
              </tr>
            </thead>
            <tbody>
              {runs.data.map((r) => (
                <tr key={r.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td><span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{r.jobName}</span></Td>
                  <Td><StatePill state={r.state} /></Td>
                  <Td className="text-text-muted">{fmtRelative(r.startedAt ?? r.createdAt)}</Td>
                  <Td>{fmtDuration(r.startedAt, r.finishedAt)}</Td>
                  <Td className="text-text-muted">{r.triggerSource}</Td>
                  <Td>
                    <Link to={`/projects/${id}/runs/${r.id}`} className="eyebrow hover:text-brand transition-colors">view →</Link>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Section>
    </>
  );
}
