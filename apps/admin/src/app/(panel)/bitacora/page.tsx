'use client';

import { DataView, EyeIcon, IconButton, Modal, PageHeader, PrimaryButton, fieldClass } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

type Entry = { id: string; occurredAt: string; actorUserId?: string; action: string; entityType: string; entityId?: string; changes?: string; ipAddress?: string };
type Audit = { items: Entry[]; nextCursor?: string };
type AccessItem = { id: string; createdAt: string; staffId: string; targetType: string; targetId: string; justification: string };
type Access = { items: AccessItem[]; nextCursor?: string };

export default function BitacoraPage() {
  const me = useMe();
  const [actor, setActor] = useState('');
  const [action, setAction] = useState('');
  const [entity, setEntity] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [cursor, setCursor] = useState<string | undefined>();
  const [accessCursor, setAccessCursor] = useState<string | undefined>();
  const [viewing, setViewing] = useState<Entry | null>(null);
  const [accessView, setAccessView] = useState<AccessItem | null>(null);
  const allowed = can(me.permissions, 'audit.view');
  const params = new URLSearchParams({ limit: '50' });
  if (actor) params.set('actorId', actor);
  if (action) params.set('action', action);
  if (entity) params.set('entityType', entity);
  if (from) params.set('from', new Date(from).toISOString());
  if (to) params.set('to', new Date(to).toISOString());
  if (cursor) params.set('cursor', cursor);
  const logs = useQuery({ queryKey: ['audit', params.toString()], queryFn: () => apiGet<Audit>(`admin/audit-logs?${params.toString()}`), enabled: allowed });
  const access = useQuery({ queryKey: ['anon-access', accessCursor], queryFn: () => apiGet<Access>(`admin/anonymous-identity-access-logs?limit=50${accessCursor ? `&cursor=${encodeURIComponent(accessCursor)}` : ''}`), enabled: allowed });
  if (!allowed) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <PageHeader title="Bitácora" hint="Acciones del personal. Ver muestra el detalle y los cambios." />
      <form className="mt-6 flex flex-wrap gap-2" onSubmit={(event) => { event.preventDefault(); setCursor(undefined); void logs.refetch(); }}>
        <input value={actor} onChange={(event) => setActor(event.target.value)} placeholder="Id del actor" aria-label="Actor" className={`${fieldClass} mt-0 w-auto`} />
        <input value={action} onChange={(event) => setAction(event.target.value)} placeholder="Acción" aria-label="Acción" className={`${fieldClass} mt-0 w-auto`} />
        <input value={entity} onChange={(event) => setEntity(event.target.value)} placeholder="Entidad" aria-label="Entidad" className={`${fieldClass} mt-0 w-auto`} />
        <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} aria-label="Desde" className={`${fieldClass} mt-0 w-auto`} />
        <input type="date" value={to} onChange={(event) => setTo(event.target.value)} aria-label="Hasta" className={`${fieldClass} mt-0 w-auto`} />
        <PrimaryButton type="submit">Filtrar</PrimaryButton>
      </form>
      <PanelState loading={logs.isLoading} error={logs.error}>
        <div className="mt-4 grid gap-3">
          <DataView
            columns={['Acción', 'Entidad', 'Cuándo']}
            emptyLabel="No hay registros con ese filtro."
            rows={(logs.data?.items ?? []).map((item) => ({
              id: item.id,
              cells: [
                <span key="action" className="font-semibold">{item.action}</span>,
                item.entityType,
                new Date(item.occurredAt).toLocaleString('es-CO'),
              ],
              actions: <IconButton label={`Ver ${item.action}`} onClick={() => setViewing(item)}><EyeIcon /></IconButton>,
            }))}
          />
          {logs.data?.nextCursor ? <button type="button" className="justify-self-start rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setCursor(logs.data?.nextCursor)}>Cargar más</button> : null}
        </div>
      </PanelState>
      <h2 className="mt-10 font-display text-xl">Accesos a identidades anónimas</h2>
      <PanelState loading={access.isLoading} error={access.error}>
        <div className="mt-4 grid gap-3">
          <DataView
            columns={['Cuándo', 'Objetivo', 'Justificación']}
            emptyLabel="No hay accesos registrados."
            rows={(access.data?.items ?? []).map((item) => ({
              id: item.id,
              cells: [
                new Date(item.createdAt).toLocaleString('es-CO'),
                item.targetType,
                <span key="why" className="line-clamp-2">{item.justification}</span>,
              ],
              actions: <IconButton label="Ver acceso" onClick={() => setAccessView(item)}><EyeIcon /></IconButton>,
            }))}
          />
          {access.data?.nextCursor ? <button type="button" className="justify-self-start rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setAccessCursor(access.data?.nextCursor)}>Cargar más</button> : null}
        </div>
      </PanelState>
      {viewing ? (
        <Modal title={viewing.action} onClose={() => setViewing(null)}>
          <dl className="grid gap-3 sm:grid-cols-2">
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Entidad</dt><dd>{viewing.entityType}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Cuándo</dt><dd>{new Date(viewing.occurredAt).toLocaleString('es-CO')}</dd></div>
            <div className="break-all"><dt className="text-xs tracking-wide text-app-muted uppercase">Actor</dt><dd>{viewing.actorUserId ?? 'sistema'}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">IP</dt><dd>{viewing.ipAddress ?? 'sin IP'}</dd></div>
            {viewing.entityId ? <div className="break-all sm:col-span-2"><dt className="text-xs tracking-wide text-app-muted uppercase">Id</dt><dd>{viewing.entityId}</dd></div> : null}
          </dl>
          {viewing.changes ? <pre className="mt-4 overflow-auto rounded-xl bg-app-surface p-3 font-mono text-xs whitespace-pre-wrap">{viewing.changes}</pre> : null}
        </Modal>
      ) : null}
      {accessView ? (
        <Modal title="Acceso a identidad anónima" onClose={() => setAccessView(null)}>
          <dl className="grid gap-3">
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Cuándo</dt><dd>{new Date(accessView.createdAt).toLocaleString('es-CO')}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Objetivo</dt><dd>{accessView.targetType}</dd></div>
            <div className="break-all"><dt className="text-xs tracking-wide text-app-muted uppercase">Id</dt><dd>{accessView.targetId}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Justificación</dt><dd>{accessView.justification}</dd></div>
          </dl>
        </Modal>
      ) : null}
    </section>
  );
}
