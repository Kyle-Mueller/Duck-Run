import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type {
  ConsoleEntry,
  Dsn,
  GlobalKpis,
  GroupDetail,
  GroupMember,
  GroupSummary,
  GroupTree,
  JobDefinition,
  Me,
  Node,
  Project,
  ProjectKpis,
  RunDetail,
  RunSummary,
} from './types';

export const qk = {
  me: ['me'] as const,
  groups: ['groups'] as const,
  groupTree: ['groups', 'tree'] as const,
  group: (id: string) => ['groups', id] as const,
  groupMembers: (id: string) => ['groups', id, 'members'] as const,
  projects: (filters?: Record<string, string | boolean | undefined>) =>
    ['projects', filters ?? {}] as const,
  project: (id: string) => ['projects', id] as const,
  projectJobs: (id: string) => ['projects', id, 'jobs'] as const,
  projectRuns: (id: string, filters?: Record<string, string | undefined>) =>
    ['projects', id, 'runs', filters ?? {}] as const,
  run: (projectId: string, runId: string) => ['projects', projectId, 'runs', runId] as const,
  runConsole: (projectId: string, runId: string) => ['projects', projectId, 'runs', runId, 'console'] as const,
  projectNodes: (id: string) => ['projects', id, 'nodes'] as const,
  projectDsns: (id: string) => ['projects', id, 'dsns'] as const,
  projectKpis: (id: string) => ['kpis', 'projects', id] as const,
  globalKpis: ['kpis', 'global'] as const,
};

export const useMe = () =>
  useQuery({
    queryKey: qk.me,
    queryFn: () => api.get<Me>('/api/auth/me'),
    retry: false,
    staleTime: 30_000,
  });

export const useLogin = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { Email: string; Password: string }) => api.post<Me>('/api/auth/login', body),
    onSuccess: (me) => qc.setQueryData(qk.me, me),
  });
};

export const useLogout = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<void>('/api/auth/logout'),
    onSuccess: () => qc.clear(),
  });
};

export const useGroups = () =>
  useQuery({ queryKey: qk.groups, queryFn: () => api.get<GroupSummary[]>('/api/groups') });

export const useGroupTree = () =>
  useQuery({ queryKey: qk.groupTree, queryFn: () => api.get<GroupTree>('/api/groups/tree') });

export const useGroup = (id: string) =>
  useQuery({
    queryKey: qk.group(id),
    queryFn: () => api.get<GroupDetail>(`/api/groups/${id}`),
    enabled: !!id,
  });

export type CreateGroupBody = {
  Name: string;
  Slug?: string;
  Description?: string;
  ParentGroupId?: string | null;
};

export const useCreateGroup = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateGroupBody) => api.post<GroupSummary>('/api/groups', body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.groups });
      qc.invalidateQueries({ queryKey: qk.groupTree });
    },
  });
};

export type UpdateGroupBody = {
  Name?: string;
  Slug?: string;
  Description?: string | null;
};

export const useUpdateGroup = (id: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateGroupBody) => api.patch<GroupSummary>(`/api/groups/${id}`, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.groups });
      qc.invalidateQueries({ queryKey: qk.groupTree });
      qc.invalidateQueries({ queryKey: qk.group(id) });
    },
  });
};

export const useDeleteGroup = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/groups/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.groups });
      qc.invalidateQueries({ queryKey: qk.groupTree });
    },
  });
};

export const useMoveGroup = (id: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (newParentGroupId: string | null) =>
      api.post<GroupSummary>(`/api/groups/${id}/move`, { NewParentGroupId: newParentGroupId }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.groups });
      qc.invalidateQueries({ queryKey: qk.groupTree });
      qc.invalidateQueries({ queryKey: qk.group(id) });
    },
  });
};

export const useGroupMembers = (id: string) =>
  useQuery({
    queryKey: qk.groupMembers(id),
    queryFn: () => api.get<GroupMember[]>(`/api/groups/${id}/members`),
    enabled: !!id,
  });

export const useAddGroupMember = (id: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { Email: string; Role: string }) =>
      api.post<GroupMember>(`/api/groups/${id}/members`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.groupMembers(id) }),
  });
};

export const useRemoveGroupMember = (groupId: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (memberId: string) => api.delete<void>(`/api/groups/${groupId}/members/${memberId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.groupMembers(groupId) }),
  });
};

export const useProjects = (params?: { groupId?: string; rootOnly?: boolean }) => {
  const filters: Record<string, string | boolean | undefined> = {};
  if (params?.groupId) filters.groupId = params.groupId;
  if (params?.rootOnly) filters.rootOnly = true;
  const qs = new URLSearchParams();
  if (params?.groupId) qs.set('groupId', params.groupId);
  if (params?.rootOnly) qs.set('rootOnly', 'true');
  const suffix = qs.toString();
  return useQuery({
    queryKey: qk.projects(filters),
    queryFn: () => api.get<Project[]>(`/api/projects${suffix ? `?${suffix}` : ''}`),
  });
};

export const useProject = (id: string) =>
  useQuery({ queryKey: qk.project(id), queryFn: () => api.get<Project>(`/api/projects/${id}`) });

export type CreateProjectBody = { Name: string; Slug?: string; GroupId?: string | null };

export const useCreateProject = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateProjectBody) => api.post<Project>('/api/projects', body),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: ['projects'] });
      qc.invalidateQueries({ queryKey: qk.groupTree });
      if (vars.GroupId) qc.invalidateQueries({ queryKey: qk.group(vars.GroupId) });
    },
  });
};

export const useMoveProject = (id: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (newGroupId: string | null) =>
      api.post<Project>(`/api/projects/${id}/move`, { NewGroupId: newGroupId }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['projects'] });
      qc.invalidateQueries({ queryKey: qk.project(id) });
      qc.invalidateQueries({ queryKey: qk.groupTree });
    },
  });
};

export const useProjectJobs = (id: string) =>
  useQuery({ queryKey: qk.projectJobs(id), queryFn: () => api.get<JobDefinition[]>(`/api/projects/${id}/jobs`) });

export const useProjectRuns = (id: string, take = 50) =>
  useQuery({
    queryKey: qk.projectRuns(id, { take: String(take) }),
    queryFn: () => api.get<RunSummary[]>(`/api/projects/${id}/runs?take=${take}`),
    refetchInterval: 3_000,
  });

export const useRun = (projectId: string, runId: string) =>
  useQuery({
    queryKey: qk.run(projectId, runId),
    queryFn: () => api.get<RunDetail>(`/api/projects/${projectId}/runs/${runId}`),
    refetchInterval: 3_000,
  });

export const useRunConsole = (projectId: string, runId: string) =>
  useQuery({
    queryKey: qk.runConsole(projectId, runId),
    queryFn: () => api.get<ConsoleEntry[]>(`/api/projects/${projectId}/runs/${runId}/console`),
    refetchInterval: 1_500,
  });

export const useProjectNodes = (id: string) =>
  useQuery({
    queryKey: qk.projectNodes(id),
    queryFn: () => api.get<Node[]>(`/api/projects/${id}/nodes`),
    refetchInterval: 5_000,
  });

export const useProjectDsns = (id: string) =>
  useQuery({ queryKey: qk.projectDsns(id), queryFn: () => api.get<Dsn[]>(`/api/projects/${id}/dsns`) });

export const useCreateDsn = (projectId: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (label: string) => api.post<Dsn>(`/api/projects/${projectId}/dsns`, { Label: label }),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.projectDsns(projectId) }),
  });
};

export const useRevokeDsn = (projectId: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (keyId: string) => api.delete<void>(`/api/projects/${projectId}/dsns/${keyId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.projectDsns(projectId) }),
  });
};

export const useProjectKpis = (id: string) =>
  useQuery({
    queryKey: qk.projectKpis(id),
    queryFn: () => api.get<ProjectKpis>(`/api/kpis/projects/${id}`),
    refetchInterval: 5_000,
  });

export const useGlobalKpis = () =>
  useQuery({
    queryKey: qk.globalKpis,
    queryFn: () => api.get<GlobalKpis>('/api/kpis/global'),
    refetchInterval: 5_000,
  });
