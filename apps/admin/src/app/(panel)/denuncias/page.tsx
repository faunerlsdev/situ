'use client';

import { DataView, EyeIcon, IconButton, PageHeader, SearchField } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

type Complaint = { id: string; targetType: string; reason: string; status: string; isPriority: boolean; createdAt: string };
type Queue = { complaints: Complaint[] };

const statusLabel: Record<string, string> = { Open: 'Abierta', InReview: 'En revisión', Upheld: 'Mantenida', Dismissed: 'Descartada' };
const targetLabel: Record<string, string> = { Report: 'Reporte', Comment: 'Comentario', User: 'Usuario' };

export default function DenunciasPage() {
  const me = useMe();
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [target, setTarget] = useState('');
  const [query, setQuery] = useState('');
  const list = useQuery({
    queryKey: ['complaints', status],
    queryFn: () => apiGet<Queue>(`moderation/complaints${status ? `?status=${status}` : ''}`),
    enabled: can(me.permissions, 'complaints.resolve'),
  });
  const rows = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return (list.data?.complaints ?? []).filter((row) => {
      if (priority !== '' && String(row.isPriority) !== priority) return false;
      if (target !== '' && row.targetType !== target) return false;
      if (!needle) return true;
      return `${row.reason} ${statusLabel[row.status] ?? row.status}`.toLowerCase().includes(needle);
    });
  }, [list.data, priority, target, query]);

  if (!can(me.permissions, 'complaints.resolve')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <PageHeader title="Denuncias" hint="Cola de moderación. Ver abre el caso completo." />
      <PanelState loading={list.isLoading} error={list.error}>
        <div className="mt-6 grid gap-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
            <SearchField value={query} onChange={setQuery} placeholder="Filtrar por motivo" />
            <div className="flex flex-wrap gap-2">
              <select aria-label="Estado" className="rounded-lg border border-app-border bg-app-input px-3 py-2 text-sm" value={status} onChange={(event) => setStatus(event.target.value)}>
                <option value="">Todos los estados</option>
                <option value="Open">Abiertas</option>
                <option value="InReview">En revisión</option>
                <option value="Upheld">Mantenidas</option>
                <option value="Dismissed">Descartadas</option>
              </select>
              <select aria-label="Prioridad" className="rounded-lg border border-app-border bg-app-input px-3 py-2 text-sm" value={priority} onChange={(event) => setPriority(event.target.value)}>
                <option value="">Toda prioridad</option>
                <option value="true">Prioritarias</option>
                <option value="false">Normales</option>
              </select>
              <select aria-label="Objetivo" className="rounded-lg border border-app-border bg-app-input px-3 py-2 text-sm" value={target} onChange={(event) => setTarget(event.target.value)}>
                <option value="">Todo objetivo</option>
                <option value="Report">Reporte</option>
                <option value="Comment">Comentario</option>
                <option value="User">Usuario</option>
              </select>
            </div>
          </div>
          <DataView
            columns={['Motivo', 'Estado', 'Objetivo', 'Fecha']}
            emptyLabel="No hay denuncias con ese filtro."
            rows={rows.map((row) => ({
              id: row.id,
              cells: [
                <span key="reason" className="font-semibold">{row.reason}{row.isPriority ? ' · prioritaria' : ''}</span>,
                statusLabel[row.status] ?? row.status,
                targetLabel[row.targetType] ?? row.targetType,
                new Date(row.createdAt).toLocaleString('es-CO'),
              ],
              actions: <IconButton label="Ver denuncia" href={`/denuncias/${row.id}`}><EyeIcon /></IconButton>,
            }))}
          />
        </div>
      </PanelState>
    </section>
  );
}
