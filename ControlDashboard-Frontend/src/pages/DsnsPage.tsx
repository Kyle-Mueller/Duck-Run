import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useCreateDsn, useProjectDsns, useRevokeDsn } from '../api/hooks';
import { Button, EmptyState, ErrorBanner, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import { fmtTime } from '../lib/format';

export function DsnsPage() {
  const { id = '' } = useParams();
  const dsns = useProjectDsns(id);
  const create = useCreateDsn(id);
  const revoke = useRevokeDsn(id);

  const [label, setLabel] = useState('');
  const [error, setError] = useState<string | null>(null);

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    create.mutate(label.trim(), {
      onSuccess: () => setLabel(''),
      onError: (err) => setError((err as Error).message),
    });
  }

  function copy(text: string) {
    void navigator.clipboard.writeText(text);
  }

  return (
    <>
      <PageHeader eyebrow="DSN" title="Keys" />

      <Section>
        <form onSubmit={submit} className="border hairline p-5 mb-8 max-w-2xl">
          <p className="eyebrow mb-3">issue a new DSN</p>
          <div className="flex gap-3">
            <input
              type="text"
              placeholder="staging-eu / primary / etc."
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              className="flex-1 px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
            <Button variant="primary" type="submit" disabled={create.isPending}>
              {create.isPending ? 'Issuing…' : 'Issue DSN'}
            </Button>
          </div>
          {error && <div className="mt-3"><ErrorBanner message={error} /></div>}
          <p className="text-text-dim text-[11px] mt-4 uppercase tracking-[0.12em]">
            paste the DSN into the runtime's <code>UseDashboard(…)</code> config — never check it into source control.
          </p>
        </form>

        {dsns.data && dsns.data.length === 0 && (
          <EmptyState message="No DSNs yet. Issue one to connect a runtime." />
        )}

        {dsns.data && dsns.data.length > 0 && (
          <Table>
            <thead>
              <tr>
                <Th>label</Th>
                <Th>DSN</Th>
                <Th className="w-32">created</Th>
                <Th className="w-32">status</Th>
                <Th></Th>
              </tr>
            </thead>
            <tbody>
              {dsns.data.map((d) => (
                <tr key={d.id}>
                  <Td><span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{d.label || '—'}</span></Td>
                  <Td>
                    <button
                      onClick={() => copy(d.dsn)}
                      className="text-text-muted hover:text-brand transition-colors text-left"
                      title="Click to copy"
                    >
                      <code style={{ fontSize: 11 }}>{d.dsn}</code>
                    </button>
                  </Td>
                  <Td className="text-text-muted whitespace-nowrap">{fmtTime(d.createdAt)}</Td>
                  <Td>
                    {d.revokedAt
                      ? <span className="pill pill-failed">revoked</span>
                      : <span className="pill pill-succeeded">active</span>}
                  </Td>
                  <Td>
                    {!d.revokedAt && (
                      <Button
                        variant="danger"
                        onClick={() => revoke.mutate(d.id)}
                        disabled={revoke.isPending}
                        className="text-[11px] px-3 py-1"
                      >
                        revoke
                      </Button>
                    )}
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
