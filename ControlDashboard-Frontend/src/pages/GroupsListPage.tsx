import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useCreateGroup, useGroupTree, useProjects } from '../api/hooks';
import { Button, EmptyState, ErrorBanner, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import type { GroupTreeNode } from '../api/types';
import { fmtRelative } from '../lib/format';

export function GroupsListPage() {
  const tree = useGroupTree();
  const rootProjects = useProjects({ rootOnly: true });
  const create = useCreateGroup();

  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!name.trim()) return;
    create.mutate(
      {
        Name: name.trim(),
        Slug: slug.trim() || undefined,
        Description: description.trim() || undefined,
        ParentGroupId: null,
      },
      {
        onSuccess: () => {
          setName('');
          setSlug('');
          setDescription('');
        },
        onError: (err) => setError((err as Error).message),
      },
    );
  }

  return (
    <>
      <PageHeader eyebrow="namespace hierarchy" title="Groups" />

      <Section>
        <form onSubmit={submit} className="border hairline p-5 mb-8 max-w-3xl">
          <p className="eyebrow mb-3">create a top-level group</p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
            <input
              type="text"
              placeholder="Acme Corp"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-heading)', borderRadius: 'var(--radius-md)' }}
            />
            <input
              type="text"
              placeholder="acme (slug, auto if blank)"
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
          </div>
          <textarea
            placeholder="optional description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={2}
            className="w-full px-3 py-2 border hairline bg-bg-input text-[13px] focus:outline-none focus:border-text mb-3"
            style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
          />
          <div className="flex items-center justify-between">
            <p className="text-text-muted text-[12px]">
              groups can nest up to 10 levels — like <code>acme/backend/billing</code>.
            </p>
            <Button variant="primary" type="submit" disabled={create.isPending || !name.trim()}>
              {create.isPending ? 'Creating…' : 'Create group'}
            </Button>
          </div>
          {error && <div className="mt-3"><ErrorBanner message={error} /></div>}
        </form>

        <div className="flex items-baseline justify-between mb-4">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.01em' }}>
            Hierarchy
          </h2>
          <span className="eyebrow">{tree.data?.roots.length ?? 0} top-level</span>
        </div>

        {tree.data && tree.data.roots.length === 0 && <EmptyState message="No groups yet. Create one above." />}

        {tree.data && tree.data.roots.length > 0 && (
          <div className="border hairline">
            {tree.data.roots.map((node) => (
              <GroupNodeRow key={node.id} node={node} level={0} />
            ))}
          </div>
        )}
      </Section>

      <Section>
        <div className="flex items-baseline justify-between mb-4">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.01em' }}>
            Ungrouped projects
          </h2>
          <span className="eyebrow">{rootProjects.data?.length ?? 0} project(s)</span>
        </div>

        {rootProjects.data && rootProjects.data.length === 0 && (
          <EmptyState message="No ungrouped projects. Every project lives inside a group." />
        )}

        {rootProjects.data && rootProjects.data.length > 0 && (
          <Table>
            <thead>
              <tr><Th>name</Th><Th>slug</Th><Th className="w-40">created</Th><Th className="w-16"></Th></tr>
            </thead>
            <tbody>
              {rootProjects.data.map((p) => (
                <tr key={p.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td>
                    <Link to={`/projects/${p.id}`} className="hover:text-brand">
                      <span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{p.name}</span>
                    </Link>
                  </Td>
                  <Td><code className="text-text-muted text-[12px]">{p.slug}</code></Td>
                  <Td className="text-text-muted">{fmtRelative(p.createdAt)}</Td>
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

function GroupNodeRow({ node, level }: { node: GroupTreeNode; level: number }) {
  const [expanded, setExpanded] = useState(level < 1);
  const hasChildren = node.subgroups.length > 0;

  return (
    <>
      <div
        className="flex items-center gap-3 px-4 py-3 border-b hairline hover:bg-bg-hover/40 transition-colors"
        style={{ paddingLeft: `${16 + level * 24}px` }}
      >
        <button
          onClick={() => setExpanded((x) => !x)}
          disabled={!hasChildren}
          className="w-4 h-4 inline-flex items-center justify-center text-text-muted disabled:opacity-30"
          style={{ fontFamily: 'var(--font-mono)' }}
        >
          {hasChildren ? (expanded ? '▾' : '▸') : '·'}
        </button>
        <Link to={`/groups/${node.id}`} className="flex-1 min-w-0 hover:text-brand">
          <div className="flex items-baseline gap-3">
            <span className="font-medium" style={{ fontFamily: 'var(--font-heading)', fontSize: 15 }}>{node.name}</span>
            <code className="text-text-muted text-[11px]">{node.fullPath}</code>
          </div>
          {node.description && (
            <p className="text-text-muted text-[12px] mt-0.5 truncate">{node.description}</p>
          )}
        </Link>
        <div className="flex items-center gap-4 text-[11px] text-text-muted shrink-0" style={{ fontFamily: 'var(--font-mono)' }}>
          <span>{node.subgroups.length} sub</span>
          <span>{node.projectCount} proj</span>
        </div>
        <Link to={`/groups/${node.id}`} className="eyebrow hover:text-brand transition-colors shrink-0">
          open →
        </Link>
      </div>
      {expanded && node.subgroups.map((child) => (
        <GroupNodeRow key={child.id} node={child} level={level + 1} />
      ))}
    </>
  );
}
