import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type {
  ConsoleEntry,
  Dsn,
  GlobalKpis,
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
  projects: ['projects'] as const,
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

export const useProjects = () =>
  useQuery({ queryKey: qk.projects, queryFn: () => api.get<Project[]>('/api/projects') });

export const useProject = (id: string) =>
  useQuery({ queryKey: qk.project(id), queryFn: () => api.get<Project>(`/api/projects/${id}`) });

export const useCreateProject = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => api.post<Project>('/api/projects', { Name: name }),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.projects }),
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
