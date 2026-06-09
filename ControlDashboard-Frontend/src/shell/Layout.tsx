import { NavLink, Outlet, useMatch } from 'react-router-dom';
import clsx from 'clsx';
import { useMe, useLogout, useProject } from '../api/hooks';

export function Layout() {
  const me = useMe();
  const logout = useLogout();
  const projectMatch = useMatch('/projects/:id/*');
  const projectId = projectMatch?.params.id;

  return (
    <div className="min-h-screen flex flex-col">
      <TopBar
        userEmail={me.data?.email ?? ''}
        onLogout={() => logout.mutate()}
      />
      <div className="flex-1 flex">
        <Sidebar projectId={projectId} />
        <main className="flex-1 min-w-0">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function TopBar({ userEmail, onLogout }: { userEmail: string; onLogout: () => void }) {
  return (
    <header
      className="border-b hairline flex items-center justify-between px-6"
      style={{ height: 'var(--nav-h)' }}
    >
      <div className="flex items-baseline gap-4">
        <NavLink to="/overview" className="flex items-baseline gap-3">
          <span
            className="font-medium tracking-tight"
            style={{ fontFamily: 'var(--font-heading)', fontSize: 20, letterSpacing: '-0.02em' }}
          >
            DuckRun
          </span>
          <span className="eyebrow hidden sm:inline">control</span>
        </NavLink>
      </div>
      <div className="flex items-center gap-4">
        <span className="text-text-muted text-[12px]">{userEmail}</span>
        <button
          onClick={onLogout}
          className="text-[12px] uppercase tracking-[0.12em] text-text-muted hover:text-brand transition-colors"
          style={{ fontFamily: 'var(--font-mono)' }}
        >
          sign out
        </button>
      </div>
    </header>
  );
}

function Sidebar({ projectId }: { projectId?: string }) {
  const project = useProject(projectId ?? '');

  if (projectId) {
    return (
      <aside className="w-60 border-r hairline shrink-0 py-6 px-4">
        <div className="px-3 pb-4 border-b hairline mb-4">
          <div className="eyebrow mb-1">project</div>
          <div className="font-medium" style={{ fontFamily: 'var(--font-heading)', fontSize: 16 }}>
            {project.data?.name ?? '—'}
          </div>
        </div>
        <NavSection>
          <NavItem to={`/projects/${projectId}`} end>Overview</NavItem>
          <NavItem to={`/projects/${projectId}/jobs`}>Jobs</NavItem>
          <NavItem to={`/projects/${projectId}/runs`}>Runs</NavItem>
          <NavItem to={`/projects/${projectId}/nodes`}>Nodes</NavItem>
          <NavItem to={`/projects/${projectId}/dsns`}>DSN &amp; keys</NavItem>
        </NavSection>
        <div className="mt-8 px-3">
          <NavLink
            to="/projects"
            className="eyebrow hover:text-brand transition-colors inline-flex items-center gap-1"
          >
            ← all projects
          </NavLink>
        </div>
      </aside>
    );
  }

  return (
    <aside className="w-60 border-r hairline shrink-0 py-6 px-4">
      <NavSection>
        <NavItem to="/overview" end>Overview</NavItem>
        <NavItem to="/groups">Groups</NavItem>
        <NavItem to="/projects">Projects</NavItem>
      </NavSection>
    </aside>
  );
}

function NavSection({ children }: { children: React.ReactNode }) {
  return <nav className="flex flex-col">{children}</nav>;
}

function NavItem({ to, end, children }: { to: string; end?: boolean; children: React.ReactNode }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        clsx(
          'px-3 py-2 text-[13px] border-l-2 transition-colors',
          isActive
            ? 'border-brand text-text bg-bg-hover/60'
            : 'border-transparent text-text-muted hover:text-text hover:bg-bg-hover/40'
        )
      }
      style={{ fontFamily: 'var(--font-mono)' }}
    >
      {children}
    </NavLink>
  );
}
