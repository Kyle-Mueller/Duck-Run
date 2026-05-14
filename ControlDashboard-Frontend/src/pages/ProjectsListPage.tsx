import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useCreateProject, useProjects } from '../api/hooks';
import { Button, EmptyState, ErrorBanner, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import { fmtTime } from '../lib/format';

export function ProjectsListPage() {
  const projects = useProjects();
  const create = useCreateProject();
  const [name, setName] = useState('');
  const [error, setError] = useState<string | null>(null);

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!name.trim()) return;
    create.mutate(name.trim(), {
      onSuccess: () => setName(''),
      onError: (err) => setError((err as Error).message),
    });
  }

  return (
    <>
      <PageHeader eyebrow="multi-tenant" title="Projects" />

      <Section>
        <form onSubmit={submit} className="border hairline p-5 mb-8 max-w-2xl">
          <p className="eyebrow mb-3">create a project</p>
          <div className="flex gap-3">
            <input
              type="text"
              placeholder="acme-billing-api"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="flex-1 px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
            <Button variant="primary" type="submit" disabled={create.isPending || !name.trim()}>
              {create.isPending ? 'Creating…' : 'Create'}
            </Button>
          </div>
          {error && <div className="mt-3"><ErrorBanner message={error} /></div>}
        </form>

        {projects.data && projects.data.length === 0 && (
          <EmptyState message="No projects yet." />
        )}

        {projects.data && projects.data.length > 0 && (
          <Table>
            <thead>
              <tr><Th>name</Th><Th className="w-40">created</Th><Th className="w-12"></Th></tr>
            </thead>
            <tbody>
              {projects.data.map((p) => (
                <tr key={p.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td>
                    <Link to={`/projects/${p.id}`} className="hover:text-brand">
                      <span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{p.name}</span>
                    </Link>
                  </Td>
                  <Td className="text-text-muted">{fmtTime(p.createdAt)}</Td>
                  <Td>
                    <Link to={`/projects/${p.id}`} className="eyebrow hover:text-brand transition-colors">open →</Link>
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
