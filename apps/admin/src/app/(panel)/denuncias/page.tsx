'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useState } from 'react';

type Queue = { complaints: { id: string; targetType: string; reason: string; status: string; isPriority: boolean; createdAt: string }[] };

export default function DenunciasPage() {
  const me = useMe();
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [target, setTarget] = useState('');
  const query = useQuery({
    queryKey: ['complaints', status],
    queryFn: () => apiGet<Queue>(`moderation/complaints${status ? `?status=${status}` : ''}`),
    enabled: can(me.permissions, 'complaints.resolve'),
  });
  if (!can(me.permissions, 'complaints.resolve')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  const rows = (query.data?.complaints ?? []).filter((row) => (priority === '' || String(row.isPriority) === priority) && (target === '' || row.targetType === target));
  return (
    <section>
      <h1 className="font-display text-3xl">Denuncias</h1>
      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:flex-wrap">
        <label className="grid gap-1">Estado<select className="rounded border border-app-border bg-app-input px-2 py-1" value={status} onChange={(event) => setStatus(event.target.value)}><option value="">Todos</option><option value="Open">Abiertas</option><option value="InReview">En revisión</option><option value="Upheld">Mantenidas</option><option value="Dismissed">Descartadas</option></select></label>
        <label className="grid gap-1">Prioridad<select className="rounded border border-app-border bg-app-input px-2 py-1" value={priority} onChange={(event) => setPriority(event.target.value)}><option value="">Todas</option><option value="true">Prioritarias</option><option value="false">Normales</option></select></label>
        <label className="grid gap-1">Objetivo<select className="rounded border border-app-border bg-app-input px-2 py-1" value={target} onChange={(event) => setTarget(event.target.value)}><option value="">Todos</option><option value="Report">Reporte</option><option value="Comment">Comentario</option><option value="User">Usuario</option></select></label>
      </div>
      <PanelState loading={query.isLoading} error={query.error} empty={rows.length === 0}>
        <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[36rem] text-left text-sm">
          <thead><tr className="text-app-muted"><th className="py-2">Motivo</th><th>Estado</th><th>Objetivo</th><th>Fecha</th></tr></thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id} className="border-t border-app-border">
                <td className="py-2"><Link href={`/denuncias/${row.id}`} className="font-semibold text-app-primary">{row.reason}{row.isPriority ? ' · prioritaria' : ''}</Link></td>
                <td>{row.status}</td><td>{row.targetType}</td><td>{new Date(row.createdAt).toLocaleString('es-CO')}</td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
      </PanelState>
    </section>
  );
}
