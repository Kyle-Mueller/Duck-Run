import { Link, useParams } from 'react-router-dom';
import { useRun, useRunConsole } from '../api/hooks';
import { PageHeader, Section, StatePill, EmptyState } from '../shell/ui';
import { fmtDuration, fmtTime, shortId } from '../lib/format';
import clsx from 'clsx';

export function RunDetailPage() {
  const { id = '', runId = '' } = useParams();
  const run = useRun(id, runId);
  const logs = useRunConsole(id, runId);

  return (
    <>
      <PageHeader
        eyebrow="run"
        title={run.data?.jobName ?? '—'}
        actions={
          <Link to={`/projects/${id}/runs`} className="eyebrow hover:text-brand transition-colors">
            ← back to runs
          </Link>
        }
      />

      <Section>
        <div className="grid grid-cols-2 md:grid-cols-5 border-t border-l hairline">
          <Meta label="state">{run.data && <StatePill state={run.data.state} />}</Meta>
          <Meta label="trigger">{run.data?.triggerSource ?? '—'}</Meta>
          <Meta label="started">{fmtTime(run.data?.startedAt ?? null)}</Meta>
          <Meta label="finished">{fmtTime(run.data?.finishedAt ?? null)}</Meta>
          <Meta label="duration">{fmtDuration(run.data?.startedAt ?? null, run.data?.finishedAt ?? null)}</Meta>
        </div>

        <div className="border-l hairline mt-6 grid grid-cols-2 md:grid-cols-2 border-t">
          <Meta label="node"><code style={{ fontSize: 12 }}>{run.data ? shortId(run.data.nodeId) : '—'}</code></Meta>
          <Meta label="run id"><code style={{ fontSize: 12, color: 'var(--text-dim)' }}>{runId}</code></Meta>
        </div>
      </Section>

      {run.data?.errorMessage && (
        <Section>
          <p className="eyebrow mb-3 text-status-fail">error</p>
          <div className="border hairline p-5 bg-[rgba(164,69,58,0.04)]">
            <div className="font-medium text-status-fail mb-2" style={{ fontFamily: 'var(--font-heading)' }}>
              {run.data.errorMessage}
            </div>
            {run.data.errorStackTrace && (
              <pre className="mt-3 text-[12px] whitespace-pre-wrap leading-relaxed text-text-muted overflow-x-auto"
                   style={{ fontFamily: 'var(--font-mono)' }}>
                {run.data.errorStackTrace}
              </pre>
            )}
          </div>
        </Section>
      )}

      <Section>
        <div className="flex items-baseline justify-between mb-3">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.02em' }}>
            Console
          </h2>
          {run.data?.state === 'Running' && <span className="eyebrow text-brand">
            <span className="brand-dot mr-2" />live
          </span>}
        </div>

        <div className="border hairline bg-bg-surface overflow-hidden" style={{ borderRadius: 'var(--radius-md)' }}>
          {logs.data && logs.data.length === 0 && <EmptyState message="No console output for this run." />}
          {logs.data && logs.data.length > 0 && (
            <pre
              className="m-0 p-5 max-h-[480px] overflow-auto text-[12px] leading-relaxed"
              style={{ fontFamily: 'var(--font-mono)', whiteSpace: 'pre-wrap' }}
            >
              {logs.data.map((e, i) => (
                <div key={i} className={clsx(
                  e.level === 'Warning' && 'text-brand',
                  e.level === 'Error' && 'text-status-fail',
                )}>
                  <span className="text-text-dim mr-3">{new Date(e.timestamp).toISOString().substring(11, 23)}</span>
                  <span className="uppercase mr-3 text-[10px]" style={{ letterSpacing: '0.08em' }}>{e.level}</span>
                  {e.message}
                </div>
              ))}
            </pre>
          )}
        </div>
      </Section>
    </>
  );
}

function Meta({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="px-5 py-4 border-r border-b hairline">
      <div className="eyebrow mb-2">{label}</div>
      <div className="text-[13px]" style={{ fontFamily: 'var(--font-mono)' }}>{children}</div>
    </div>
  );
}
