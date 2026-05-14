import { useParams } from 'react-router-dom';
import { useProjectJobs } from '../api/hooks';
import { EmptyState, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import { fmtRelative } from '../lib/format';

export function JobsPage() {
  const { id = '' } = useParams();
  const jobs = useProjectJobs(id);

  return (
    <>
      <PageHeader eyebrow="registered" title="Jobs" />
      <Section>
        {jobs.data && jobs.data.length === 0 && (
          <EmptyState message="No jobs registered yet. Connect a runtime via DSN to see jobs appear here." />
        )}

        {jobs.data && jobs.data.length > 0 && (
          <Table>
            <thead>
              <tr>
                <Th>name</Th>
                <Th>cron</Th>
                <Th>max concurrency</Th>
                <Th>timeout</Th>
                <Th>enabled</Th>
                <Th>last seen</Th>
              </tr>
            </thead>
            <tbody>
              {jobs.data.map((j) => (
                <tr key={j.name}>
                  <Td><span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{j.name}</span></Td>
                  <Td><code style={{ fontFamily: 'var(--font-mono)' }}>{j.cron}</code></Td>
                  <Td>{j.maxConcurrency}</Td>
                  <Td>{j.timeoutSeconds > 0 ? `${j.timeoutSeconds}s` : '—'}</Td>
                  <Td>
                    {j.enabled
                      ? <span className="text-status-ok">yes</span>
                      : <span className="text-text-muted">no</span>}
                  </Td>
                  <Td className="text-text-muted">{fmtRelative(j.lastSeen)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Section>
    </>
  );
}
