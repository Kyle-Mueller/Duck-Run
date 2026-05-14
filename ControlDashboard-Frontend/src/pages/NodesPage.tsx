import { useParams } from 'react-router-dom';
import { useProjectNodes } from '../api/hooks';
import { EmptyState, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import { fmtRelative, shortId } from '../lib/format';
import clsx from 'clsx';

const ALIVE_MS = 60_000;

export function NodesPage() {
  const { id = '' } = useParams();
  const nodes = useProjectNodes(id);

  return (
    <>
      <PageHeader eyebrow="cluster" title="Nodes" />
      <Section>
        {nodes.data && nodes.data.length === 0 && (
          <EmptyState message="No nodes have reported yet. Once a runtime connects via its DSN, it'll show up here." />
        )}

        {nodes.data && nodes.data.length > 0 && (
          <Table>
            <thead>
              <tr>
                <Th></Th>
                <Th>node id</Th>
                <Th>runtime</Th>
                <Th>version</Th>
                <Th>leader</Th>
                <Th>started</Th>
                <Th>last seen</Th>
              </tr>
            </thead>
            <tbody>
              {nodes.data.map((n) => {
                const alive = Date.now() - new Date(n.lastSeen).getTime() < ALIVE_MS;
                return (
                  <tr key={n.nodeId}>
                    <Td className="w-8">
                      <span className={clsx(
                        'inline-block w-2 h-2 rounded-full',
                        alive ? 'bg-status-ok' : 'bg-status-neutral'
                      )} />
                    </Td>
                    <Td><code style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>{shortId(n.nodeId)}</code></Td>
                    <Td className="text-text-muted">{n.runtime || '—'}</Td>
                    <Td className="text-text-muted">{n.clientVersion || '—'}</Td>
                    <Td>
                      {n.isLeader && alive
                        ? <span className="pill pill-running"><span className="brand-dot" />leader</span>
                        : <span className="text-text-muted">—</span>}
                    </Td>
                    <Td className="text-text-muted">{fmtRelative(n.startedAt)}</Td>
                    <Td className="text-text-muted">{fmtRelative(n.lastSeen)}</Td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
        )}
      </Section>
    </>
  );
}
