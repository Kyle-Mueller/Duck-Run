import { Link, useParams } from 'react-router-dom';
import { useProjectRuns } from '../api/hooks';
import { EmptyState, PageHeader, Section, StatePill, Table, Td, Th } from '../shell/ui';
import { fmtDuration, fmtTime, shortId } from '../lib/format';

export function RunsPage() {
  const { id = '' } = useParams();
  const runs = useProjectRuns(id, 100);

  return (
    <>
      <PageHeader eyebrow="last 100" title="Runs" />
      <Section>
        {runs.data && runs.data.length === 0 && <EmptyState message="No runs yet." />}

        {runs.data && runs.data.length > 0 && (
          <Table>
            <thead>
              <tr>
                <Th>started</Th>
                <Th>job</Th>
                <Th>state</Th>
                <Th>duration</Th>
                <Th>trigger</Th>
                <Th>node</Th>
                <Th>run id</Th>
                <Th></Th>
              </tr>
            </thead>
            <tbody>
              {runs.data.map((r) => (
                <tr key={r.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td className="text-text-muted whitespace-nowrap">{fmtTime(r.startedAt ?? r.createdAt)}</Td>
                  <Td><span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{r.jobName}</span></Td>
                  <Td><StatePill state={r.state} /></Td>
                  <Td>{fmtDuration(r.startedAt, r.finishedAt)}</Td>
                  <Td className="text-text-muted">{r.triggerSource}</Td>
                  <Td className="text-text-muted"><code style={{ fontSize: 11 }}>{shortId(r.nodeId)}</code></Td>
                  <Td><code style={{ fontSize: 11, color: 'var(--text-dim)' }}>{shortId(r.id)}</code></Td>
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
