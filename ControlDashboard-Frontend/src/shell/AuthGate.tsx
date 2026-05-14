import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useMe } from '../api/hooks';

export function AuthGate({ children }: { children: ReactNode }) {
  const me = useMe();
  const location = useLocation();

  if (me.isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <span className="eyebrow">loading session</span>
      </div>
    );
  }

  if (me.isError || !me.data) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
}
