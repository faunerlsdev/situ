'use client';

import { Shell, type AdminMe } from '@/components/shell';
import { apiGet } from '@/lib/client';
import { classifyStatus } from '@/lib/guard';
import { PanelError } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { createContext, useContext, useEffect, type ReactNode } from 'react';

const MeContext = createContext<AdminMe | null>(null);

export function useMe() {
  const me = useContext(MeContext);
  if (!me) {
    throw new Error('La sesión del panel no está lista.');
  }

  return me;
}

export function Frame({ env, children }: { env: string; children: ReactNode }) {
  const router = useRouter();
  const me = useQuery({ queryKey: ['admin-me'], queryFn: () => apiGet<AdminMe>('admin/me') });
  useEffect(() => {
    if (me.error instanceof PanelError && classifyStatus(me.error.status) === 'session') {
      router.replace('/login');
    }
  }, [me.error, router]);

  if (!me.data) {
    return <p className="p-8 text-app-muted">{me.error instanceof Error && me.error.message ? me.error.message : 'Cargando el panel…'}</p>;
  }

  return (
    <MeContext.Provider value={me.data}>
      <Shell env={env} me={me.data}>{children}</Shell>
    </MeContext.Provider>
  );
}
