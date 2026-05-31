import clsx from 'clsx';
import type { ReactNode } from 'react';

export function PageHeader({
  eyebrow,
  title,
  actions,
}: {
  eyebrow?: ReactNode;
  title: string;
  actions?: ReactNode;
}) {
  return (
    <div className="border-b hairline px-8 py-7 flex items-start justify-between gap-6">
      <div>
        {eyebrow && <p className="eyebrow mb-2">{eyebrow}</p>}
        <h1 style={{ fontSize: 26, letterSpacing: '-0.02em', fontWeight: 500 }}>{title}</h1>
      </div>
      {actions && <div className="flex items-center gap-3">{actions}</div>}
    </div>
  );
}

export function Section({ children }: { children: ReactNode }) {
  return <section className="px-8 py-7">{children}</section>;
}

export function Button({
  variant = 'secondary',
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' }) {
  return (
    <button
      {...props}
      className={clsx(
        'inline-flex items-center justify-center px-4 py-2 text-[13px] font-medium transition-colors',
        'rounded-[var(--radius-md)] border',
        variant === 'primary' && 'bg-text text-bg border-text hover:bg-brand hover:border-brand',
        variant === 'secondary' && 'bg-transparent text-text border-text-muted/40 hover:border-text hover:bg-bg-hover/40',
        variant === 'danger' && 'bg-transparent text-status-fail border-status-fail/40 hover:bg-status-fail/10',
        'disabled:opacity-50 disabled:cursor-not-allowed',
        className,
      )}
      style={{ fontFamily: 'var(--font-display)', letterSpacing: '0.01em' }}
    />
  );
}

export function KpiCard({
  label,
  value,
  hint,
  emphasised = false,
}: {
  label: string;
  value: ReactNode;
  hint?: ReactNode;
  emphasised?: boolean;
}) {
  return (
    <div className={clsx('px-6 py-7 border-r border-b hairline', emphasised && 'bg-bg-surface')}>
      <div className="eyebrow mb-3">{label}</div>
      <div
        className="font-medium"
        style={{ fontFamily: 'var(--font-mono)', fontSize: 32, lineHeight: 1, letterSpacing: '-0.02em' }}
      >
        {value}
      </div>
      {hint && <div className="text-text-muted text-[12px] mt-3">{hint}</div>}
    </div>
  );
}

export function StatePill({ state }: { state: string }) {
  const cls = `pill pill-${state.toLowerCase()}`;
  return <span className={cls}>{state}</span>;
}

export function EmptyState({ message }: { message: string }) {
  return <div className="text-text-muted text-[13px] py-8 text-center">{message}</div>;
}

export function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="border hairline px-4 py-3 mb-4 text-[13px] text-status-fail bg-[rgba(164,69,58,0.06)]">
      {message}
    </div>
  );
}

export function Table({ children }: { children: ReactNode }) {
  return (
    <div className="border-t border-l hairline">
      <table className="w-full border-collapse text-[13px]">{children}</table>
    </div>
  );
}

export function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return (
    <th
      className={clsx(
        'text-left px-4 py-3 border-r border-b hairline text-text-muted font-medium uppercase tracking-[0.1em]',
        className,
      )}
      style={{ fontFamily: 'var(--font-display)', fontSize: 10, letterSpacing: '0.12em' }}
    >
      {children}
    </th>
  );
}

export function Td({ children, className }: { children?: ReactNode; className?: string }) {
  return (
    <td className={clsx('px-4 py-3 border-r border-b hairline align-middle', className)}>{children}</td>
  );
}
