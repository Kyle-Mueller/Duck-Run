import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useLogin, useMe } from '../api/hooks';
import { Button, ErrorBanner } from '../shell/ui';

export function LoginPage() {
  const me = useMe();
  const login = useLogin();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const location = useLocation();

  if (me.data) {
    const from = (location.state as { from?: { pathname: string } } | undefined)?.from?.pathname ?? '/overview';
    return <Navigate to={from} replace />;
  }

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    login.mutate({ Email: email, Password: password }, {
      onError: (err) => setError((err as Error).message || 'Sign in failed.'),
    });
  }

  return (
    <div className="min-h-screen flex">
      <aside
        className="hidden lg:flex flex-col justify-between border-r hairline px-12 py-14"
        style={{ width: '46%', background: 'var(--bg-surface)' }}
      >
        <div>
          <div className="flex items-baseline gap-3 mb-16">
            <span className="font-medium" style={{ fontFamily: 'var(--font-heading)', fontSize: 22, letterSpacing: '-0.02em' }}>DuckRun</span>
            <span className="eyebrow">control dashboard</span>
          </div>
          <h1
            style={{
              fontFamily: 'var(--font-heading)',
              fontSize: 'clamp(2.25rem, 4vw, 3.25rem)',
              fontWeight: 500,
              lineHeight: 1.05,
              letterSpacing: '-0.03em',
              maxWidth: 520,
            }}
          >
            Cron, runs, console — for as many apps as you've got.
          </h1>
          <p className="mt-7 text-text-muted text-[14px] leading-relaxed" style={{ maxWidth: 480 }}>
            One dashboard. Many runtimes. We replace Hangfire's split-brain UI with a Sentry-style DSN handshake
            and a single page that knows when your job last actually finished.
          </p>
        </div>
        <div className="text-[11px] text-text-dim uppercase tracking-[0.14em]">
          <span className="inline-block w-1.5 h-1.5 rounded-full bg-[var(--status-ok)] mr-2 align-middle" />
          local cookie · pre-1.0 alpha
        </div>
      </aside>

      <main className="flex-1 flex items-center justify-center px-6 py-12">
        <form onSubmit={submit} className="w-full max-w-sm">
          <p className="eyebrow mb-3">sign in</p>
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 28, fontWeight: 500, letterSpacing: '-0.02em', marginBottom: 28 }}>
            Welcome back.
          </h2>

          {error && <ErrorBanner message={error} />}

          <label className="block mb-5">
            <div className="eyebrow mb-2">email</div>
            <input
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-3 py-2.5 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
          </label>

          <label className="block mb-7">
            <div className="eyebrow mb-2">password</div>
            <input
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2.5 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
          </label>

          <Button variant="primary" type="submit" className="w-full" disabled={login.isPending}>
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </Button>

          <p className="text-text-dim text-[11px] mt-6 uppercase tracking-[0.12em]">
            no signup. an administrator created your account.
          </p>
        </form>
      </main>
    </div>
  );
}
