import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useCreateProject, useGroups, useProjects } from '../api/hooks';
import { Button, EmptyState, ErrorBanner, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import { fmtTime } from '../lib/format';

const ROOT = '__root__';

export function ProjectsListPage() {
  const groups = useGroups();
  const [filter, setFilter] = useState<string>(''); // '' = all, ROOT = ungrouped, or group id

  const projectParams = useMemo(() => {
    if (filter === '') return undefined;
    if (filter === ROOT) return { rootOnly: true };
    return { groupId: filter };
  }, [filter]);

  const projects = useProjects(projectParams);
  const create = useCreateProject();

  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [createGroupId, setCreateGroupId] = useState<string>(ROOT);
  const [error, setError] = useState<string | null>(null);

  const groupPathById = useMemo(() => {
    const map = new Map<string, string>();
    (groups.data ?? []).forEach((g) => map.set(g.id, g.fullPath));
    return map;
  }, [groups.data]);

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!name.trim()) return;
    create.mutate(
      {
        Name: name.trim(),
        Slug: slug.trim() || undefined,
        GroupId: createGroupId === ROOT ? null : createGroupId,
      },
      {
        onSuccess: () => {
          setName('');
          setSlug('');
        },
        onError: (err) => setError((err as Error).message),
      },
    );
  }

  return (
    <>
      <PageHeader eyebrow="multi-tenant" title="Projects" />

      <Section>
        <form onSubmit={submit} className="border hairline p-5 mb-8 max-w-3xl">
          <p className="eyebrow mb-3">create a project</p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
            <input
              type="text"
              placeholder="acme-billing-api"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
            <input
              type="text"
              placeholder="slug (auto if blank)"
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
          </div>
          <div className="flex gap-3 items-center">
            <label className="eyebrow shrink-0">group</label>
            <select
              value={createGroupId}
              onChange={(e) => setCreateGroupId(e.target.value)}
              className="flex-1 px-3 py-2 border hairline bg-bg-input text-[13px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            >
              <option value={ROOT}>— ungrouped (top level) —</option>
              {(groups.data ?? []).map((g) => (
                <option key={g.id} value={g.id}>{g.fullPath}</option>
              ))}
            </select>
            <Button variant="primary" type="submit" disabled={create.isPending || !name.trim()}>
              {create.isPending ? 'Creating…' : 'Create'}
            </Button>
          </div>
          {error && <div className="mt-3"><ErrorBanner message={error} /></div>}
        </form>

        <div className="flex items-center gap-3 mb-4">
          <label className="eyebrow shrink-0">filter</label>
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            className="px-3 py-2 border hairline bg-bg-input text-[13px] focus:outline-none focus:border-text"
            style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
          >
            <option value="">all projects</option>
            <option value={ROOT}>ungrouped only</option>
            {(groups.data ?? []).map((g) => (
              <option key={g.id} value={g.id}>{g.fullPath}</option>
            ))}
          </select>
        </div>

        {projects.data && projects.data.length === 0 && (
          <EmptyState message="No projects match." />
        )}

        {projects.data && projects.data.length > 0 && (
          <Table>
            <thead>
              <tr><Th>name</Th><Th>location</Th><Th className="w-40">created</Th><Th className="w-12"></Th></tr>
            </thead>
            <tbody>
              {projects.data.map((p) => (
                <tr key={p.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td>
                    <Link to={`/projects/${p.id}`} className="hover:text-brand">
                      <span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{p.name}</span>
                    </Link>
                    <div className="text-text-muted text-[11px] mt-0.5"><code>{p.slug}</code></div>
                  </Td>
                  <Td>
                    {p.groupId ? (
                      <Link to={`/groups/${p.groupId}`} className="hover:text-brand">
                        <code className="text-[12px]">{groupPathById.get(p.groupId) ?? '—'}</code>
                      </Link>
                    ) : (
                      <span className="text-text-muted text-[12px]">ungrouped</span>
                    )}
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
