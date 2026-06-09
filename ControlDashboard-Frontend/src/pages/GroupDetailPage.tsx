import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  useAddGroupMember,
  useCreateGroup,
  useCreateProject,
  useDeleteGroup,
  useGroup,
  useGroupMembers,
  useGroups,
  useMoveGroup,
  useRemoveGroupMember,
  useUpdateGroup,
} from '../api/hooks';
import { Button, EmptyState, ErrorBanner, PageHeader, Section, Table, Td, Th } from '../shell/ui';
import { fmtRelative, fmtTime } from '../lib/format';
import type { GroupSummary } from '../api/types';

export function GroupDetailPage() {
  const { id = '' } = useParams();
  const nav = useNavigate();
  const group = useGroup(id);
  const allGroups = useGroups();
  const members = useGroupMembers(id);

  const createSubgroup = useCreateGroup();
  const createProject = useCreateProject();
  const updateGroup = useUpdateGroup(id);
  const moveGroup = useMoveGroup(id);
  const deleteGroup = useDeleteGroup();
  const addMember = useAddGroupMember(id);
  const removeMember = useRemoveGroupMember(id);

  const [subgroupName, setSubgroupName] = useState('');
  const [subgroupSlug, setSubgroupSlug] = useState('');
  const [subgroupErr, setSubgroupErr] = useState<string | null>(null);

  const [projectName, setProjectName] = useState('');
  const [projectSlug, setProjectSlug] = useState('');
  const [projectErr, setProjectErr] = useState<string | null>(null);

  const [editName, setEditName] = useState('');
  const [editSlug, setEditSlug] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editOpen, setEditOpen] = useState(false);
  const [editErr, setEditErr] = useState<string | null>(null);

  const [moveErr, setMoveErr] = useState<string | null>(null);

  const [memberEmail, setMemberEmail] = useState('');
  const [memberRole, setMemberRole] = useState('Viewer');
  const [memberErr, setMemberErr] = useState<string | null>(null);

  function openEdit() {
    if (!group.data) return;
    setEditName(group.data.name);
    setEditSlug(group.data.slug);
    setEditDescription(group.data.description ?? '');
    setEditErr(null);
    setEditOpen(true);
  }

  function submitSubgroup(e: React.FormEvent) {
    e.preventDefault();
    setSubgroupErr(null);
    if (!subgroupName.trim()) return;
    createSubgroup.mutate(
      { Name: subgroupName.trim(), Slug: subgroupSlug.trim() || undefined, ParentGroupId: id },
      {
        onSuccess: () => {
          setSubgroupName('');
          setSubgroupSlug('');
        },
        onError: (err) => setSubgroupErr((err as Error).message),
      },
    );
  }

  function submitProject(e: React.FormEvent) {
    e.preventDefault();
    setProjectErr(null);
    if (!projectName.trim()) return;
    createProject.mutate(
      { Name: projectName.trim(), Slug: projectSlug.trim() || undefined, GroupId: id },
      {
        onSuccess: () => {
          setProjectName('');
          setProjectSlug('');
        },
        onError: (err) => setProjectErr((err as Error).message),
      },
    );
  }

  function submitEdit(e: React.FormEvent) {
    e.preventDefault();
    setEditErr(null);
    updateGroup.mutate(
      { Name: editName.trim(), Slug: editSlug.trim() || undefined, Description: editDescription.trim() },
      {
        onSuccess: () => setEditOpen(false),
        onError: (err) => setEditErr((err as Error).message),
      },
    );
  }

  function submitMove(newParent: string | null) {
    setMoveErr(null);
    moveGroup.mutate(newParent, {
      onError: (err) => setMoveErr((err as Error).message),
    });
  }

  function submitDelete() {
    if (!confirm('Delete this group? It must be empty (no subgroups, no projects).')) return;
    deleteGroup.mutate(id, {
      onSuccess: () => nav('/groups'),
      onError: (err) => alert((err as Error).message),
    });
  }

  function submitAddMember(e: React.FormEvent) {
    e.preventDefault();
    setMemberErr(null);
    if (!memberEmail.trim()) return;
    addMember.mutate(
      { Email: memberEmail.trim(), Role: memberRole },
      {
        onSuccess: () => setMemberEmail(''),
        onError: (err) => setMemberErr((err as Error).message),
      },
    );
  }

  if (!group.data) {
    return (
      <>
        <PageHeader eyebrow="group" title="—" />
        <Section><EmptyState message={group.isLoading ? 'Loading…' : 'Group not found.'} /></Section>
      </>
    );
  }

  const moveCandidates = (allGroups.data ?? []).filter((g) => !isSelfOrDescendant(g, group.data!.fullPath));

  return (
    <>
      <PageHeader
        eyebrow={
          <Breadcrumbs ancestors={group.data.ancestors} self={group.data.name} />
        }
        title={group.data.name}
        actions={
          <>
            <Button variant="secondary" type="button" onClick={openEdit}>Edit</Button>
            <Button variant="danger" type="button" onClick={submitDelete}>Delete</Button>
          </>
        }
      />

      <Section>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-2">
          <Stat label="full path" value={<code className="text-[13px]">{group.data.fullPath}</code>} />
          <Stat label="depth" value={String(group.data.depth)} />
          <Stat label="created" value={fmtTime(group.data.createdAt)} />
        </div>
        {group.data.description && (
          <div className="border hairline p-4 mt-4 max-w-3xl">
            <p className="eyebrow mb-2">description</p>
            <p className="text-[14px]" style={{ fontFamily: 'var(--font-heading)' }}>{group.data.description}</p>
          </div>
        )}
      </Section>

      {editOpen && (
        <Section>
          <form onSubmit={submitEdit} className="border hairline p-5 max-w-3xl bg-bg-surface">
            <p className="eyebrow mb-3">edit group</p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
              <input
                type="text"
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                placeholder="display name"
                className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
                style={{ fontFamily: 'var(--font-heading)', borderRadius: 'var(--radius-md)' }}
              />
              <input
                type="text"
                value={editSlug}
                onChange={(e) => setEditSlug(e.target.value)}
                placeholder="slug"
                className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
                style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
              />
            </div>
            <textarea
              value={editDescription}
              onChange={(e) => setEditDescription(e.target.value)}
              placeholder="description"
              rows={2}
              className="w-full px-3 py-2 border hairline bg-bg-input text-[13px] focus:outline-none focus:border-text mb-3"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
            <div className="flex items-center gap-3">
              <Button variant="primary" type="submit" disabled={updateGroup.isPending}>
                {updateGroup.isPending ? 'Saving…' : 'Save'}
              </Button>
              <Button variant="secondary" type="button" onClick={() => setEditOpen(false)}>Cancel</Button>
            </div>
            {editErr && <div className="mt-3"><ErrorBanner message={editErr} /></div>}
          </form>
        </Section>
      )}

      <Section>
        <div className="flex items-baseline justify-between mb-4">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.01em' }}>
            Move group
          </h2>
          <span className="eyebrow">change parent</span>
        </div>
        <div className="border hairline p-4 max-w-3xl flex items-center gap-3">
          <select
            onChange={(e) => submitMove(e.target.value === '__root__' ? null : e.target.value)}
            value={group.data.parentGroupId ?? '__root__'}
            disabled={moveGroup.isPending}
            className="flex-1 px-3 py-2 border hairline bg-bg-input text-[13px] focus:outline-none focus:border-text"
            style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
          >
            <option value="__root__">— top level —</option>
            {moveCandidates.map((g) => (
              <option key={g.id} value={g.id}>{g.fullPath}</option>
            ))}
          </select>
          {moveGroup.isPending && <span className="text-text-muted text-[12px]">moving…</span>}
        </div>
        {moveErr && <div className="mt-3 max-w-3xl"><ErrorBanner message={moveErr} /></div>}
      </Section>

      <Section>
        <div className="flex items-baseline justify-between mb-4">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.01em' }}>
            Subgroups
          </h2>
          <span className="eyebrow">{group.data.subgroups.length} subgroup(s)</span>
        </div>

        <form onSubmit={submitSubgroup} className="border hairline p-5 mb-6 max-w-3xl">
          <p className="eyebrow mb-3">create a subgroup</p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
            <input
              type="text"
              placeholder="Billing"
              value={subgroupName}
              onChange={(e) => setSubgroupName(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-heading)', borderRadius: 'var(--radius-md)' }}
            />
            <input
              type="text"
              placeholder="billing (slug, auto if blank)"
              value={subgroupSlug}
              onChange={(e) => setSubgroupSlug(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
          </div>
          <div className="flex items-center justify-between">
            <p className="text-text-muted text-[12px]">creates <code>{group.data.fullPath}/{'<slug>'}</code></p>
            <Button variant="primary" type="submit" disabled={createSubgroup.isPending || !subgroupName.trim()}>
              {createSubgroup.isPending ? 'Creating…' : 'Add subgroup'}
            </Button>
          </div>
          {subgroupErr && <div className="mt-3"><ErrorBanner message={subgroupErr} /></div>}
        </form>

        {group.data.subgroups.length === 0 && <EmptyState message="No subgroups." />}

        {group.data.subgroups.length > 0 && (
          <Table>
            <thead>
              <tr><Th>name</Th><Th>path</Th><Th className="w-40">created</Th><Th className="w-16"></Th></tr>
            </thead>
            <tbody>
              {group.data.subgroups.map((sg) => (
                <tr key={sg.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td>
                    <Link to={`/groups/${sg.id}`} className="hover:text-brand">
                      <span className="font-medium" style={{ fontFamily: 'var(--font-heading)' }}>{sg.name}</span>
                    </Link>
                  </Td>
                  <Td><code className="text-text-muted text-[12px]">{sg.fullPath}</code></Td>
                  <Td className="text-text-muted">{fmtRelative(sg.createdAt)}</Td>
                  <Td>
                    <Link to={`/groups/${sg.id}`} className="eyebrow hover:text-brand transition-colors">open →</Link>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Section>

      <Section>
        <div className="flex items-baseline justify-between mb-4">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.01em' }}>
            Projects in this group
          </h2>
          <span className="eyebrow">{group.data.projects.length} project(s)</span>
        </div>

        <form onSubmit={submitProject} className="border hairline p-5 mb-6 max-w-3xl">
          <p className="eyebrow mb-3">create a project here</p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
            <input
              type="text"
              placeholder="acme-billing-api"
              value={projectName}
              onChange={(e) => setProjectName(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
            <input
              type="text"
              placeholder="slug (auto if blank)"
              value={projectSlug}
              onChange={(e) => setProjectSlug(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
          </div>
          <div className="flex items-center justify-end">
            <Button variant="primary" type="submit" disabled={createProject.isPending || !projectName.trim()}>
              {createProject.isPending ? 'Creating…' : 'Add project'}
            </Button>
          </div>
          {projectErr && <div className="mt-3"><ErrorBanner message={projectErr} /></div>}
        </form>

        {group.data.projects.length === 0 && <EmptyState message="No projects in this group." />}

        {group.data.projects.length > 0 && (
          <Table>
            <thead>
              <tr><Th>name</Th><Th>slug</Th><Th className="w-40">created</Th><Th className="w-16"></Th></tr>
            </thead>
            <tbody>
              {group.data.projects.map((p) => (
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

      <Section>
        <div className="flex items-baseline justify-between mb-4">
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: 18, fontWeight: 500, letterSpacing: '-0.01em' }}>
            Members
          </h2>
          <span className="eyebrow">{members.data?.length ?? 0} member(s)</span>
        </div>

        <form onSubmit={submitAddMember} className="border hairline p-5 mb-6 max-w-3xl">
          <p className="eyebrow mb-3">add a member</p>
          <div className="flex gap-3 mb-3">
            <input
              type="email"
              placeholder="user@example.com"
              value={memberEmail}
              onChange={(e) => setMemberEmail(e.target.value)}
              className="flex-1 px-3 py-2 border hairline bg-bg-input text-[14px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            />
            <select
              value={memberRole}
              onChange={(e) => setMemberRole(e.target.value)}
              className="px-3 py-2 border hairline bg-bg-input text-[13px] focus:outline-none focus:border-text"
              style={{ fontFamily: 'var(--font-mono)', borderRadius: 'var(--radius-md)' }}
            >
              <option value="Admin">Admin</option>
              <option value="Maintainer">Maintainer</option>
              <option value="Developer">Developer</option>
              <option value="Viewer">Viewer</option>
            </select>
            <Button variant="primary" type="submit" disabled={addMember.isPending || !memberEmail.trim()}>
              {addMember.isPending ? 'Adding…' : 'Add'}
            </Button>
          </div>
          {memberErr && <ErrorBanner message={memberErr} />}
        </form>

        {members.data && members.data.length === 0 && <EmptyState message="No members yet." />}

        {members.data && members.data.length > 0 && (
          <Table>
            <thead>
              <tr><Th>email</Th><Th>name</Th><Th className="w-32">role</Th><Th className="w-40">added</Th><Th className="w-16"></Th></tr>
            </thead>
            <tbody>
              {members.data.map((m) => (
                <tr key={m.id} className="hover:bg-bg-hover/40 transition-colors">
                  <Td><code className="text-[12px]">{m.email}</code></Td>
                  <Td>{m.displayName || '—'}</Td>
                  <Td><span className="eyebrow">{m.role}</span></Td>
                  <Td className="text-text-muted">{fmtRelative(m.addedAt)}</Td>
                  <Td>
                    <button
                      onClick={() => removeMember.mutate(m.id)}
                      className="eyebrow text-status-fail hover:opacity-80 transition-opacity"
                    >
                      remove
                    </button>
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

function isSelfOrDescendant(candidate: GroupSummary, selfPath: string): boolean {
  return candidate.fullPath === selfPath || candidate.fullPath.startsWith(selfPath + '/');
}

function Breadcrumbs({ ancestors, self }: { ancestors: { id: string; name: string }[]; self: string }) {
  return (
    <span className="inline-flex items-baseline gap-1">
      <Link to="/groups" className="hover:text-brand">groups</Link>
      {ancestors.map((a) => (
        <span key={a.id} className="inline-flex items-baseline gap-1">
          <span className="text-text-muted">/</span>
          <Link to={`/groups/${a.id}`} className="hover:text-brand">{a.name}</Link>
        </span>
      ))}
      <span className="text-text-muted">/</span>
      <span>{self}</span>
    </span>
  );
}

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="border hairline px-5 py-4">
      <p className="eyebrow mb-2">{label}</p>
      <div style={{ fontFamily: 'var(--font-heading)', fontSize: 17, fontWeight: 500 }}>{value}</div>
    </div>
  );
}
