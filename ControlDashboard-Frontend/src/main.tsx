import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import './styles/globals.css';

import { AuthGate } from './shell/AuthGate';
import { Layout } from './shell/Layout';

import { LoginPage } from './pages/LoginPage';
import { GlobalOverviewPage } from './pages/GlobalOverviewPage';
import { ProjectsListPage } from './pages/ProjectsListPage';
import { ProjectOverviewPage } from './pages/ProjectOverviewPage';
import { JobsPage } from './pages/JobsPage';
import { RunsPage } from './pages/RunsPage';
import { RunDetailPage } from './pages/RunDetailPage';
import { NodesPage } from './pages/NodesPage';
import { DsnsPage } from './pages/DsnsPage';

const qc = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={qc}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<AuthGate><Layout /></AuthGate>}>
            <Route index element={<Navigate to="/overview" replace />} />
            <Route path="/overview" element={<GlobalOverviewPage />} />
            <Route path="/projects" element={<ProjectsListPage />} />
            <Route path="/projects/:id" element={<ProjectOverviewPage />} />
            <Route path="/projects/:id/jobs" element={<JobsPage />} />
            <Route path="/projects/:id/runs" element={<RunsPage />} />
            <Route path="/projects/:id/runs/:runId" element={<RunDetailPage />} />
            <Route path="/projects/:id/nodes" element={<NodesPage />} />
            <Route path="/projects/:id/dsns" element={<DsnsPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/overview" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>
);
