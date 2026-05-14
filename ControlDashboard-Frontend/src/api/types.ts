export type Me = {
  id: string;
  email: string;
  displayName: string;
  role: string;
  lastSignInAt?: string | null;
};

export type Project = {
  id: string;
  name: string;
  createdAt: string;
};

export type JobDefinition = {
  name: string;
  cron: string;
  maxConcurrency: number;
  timeoutSeconds: number;
  allowManualTrigger: boolean;
  enabled: boolean;
  firstSeen: string;
  lastSeen: string;
};

export type RunSummary = {
  id: string;
  jobName: string;
  nodeId: string;
  state: string;
  triggerSource: string;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  hasError: boolean;
};

export type RunDetail = RunSummary & {
  errorMessage: string | null;
  errorStackTrace: string | null;
};

export type ConsoleEntry = {
  timestamp: string;
  level: string;
  message: string;
};

export type Node = {
  nodeId: string;
  runtime: string;
  clientVersion: string;
  startedAt: string;
  lastSeen: string;
  isLeader: boolean;
};

export type Dsn = {
  id: string;
  publicKey: string;
  label: string;
  createdAt: string;
  revokedAt: string | null;
  dsn: string;
};

export type StateCount = { state: string; count: number };

export type ProjectKpis = {
  jobs: number;
  nodesAlive: number;
  nodesTotal: number;
  leader: string | null;
  running: number;
  runs24h: StateCount[];
};

export type GlobalKpis = {
  totalProjects: number;
  totalJobs: number;
  activeNodes: number;
  running: number;
  runs24h: StateCount[];
};
