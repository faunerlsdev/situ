'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

type Audit = { items: { id: string; occurredAt: string; actorUserId?: string; action: string; entityType: string; entityId?: string; changes?: string; ipAddress?: string }[]; nextCursor?: string };
type Access = { items: { id: string; createdAt: string; staffId: string; targetType: string; targetId: string; justification: string }[]; nextCursor?: string };

export default function BitacoraPage() {
  const me = useMe();
  const [actor, setActor] = useState('');
  const [action, setAction] = useState('');
  const [entity, setEntity] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [cursor, setCursor] = useState<string | undefined>();
  const [accessCursor, setAccessCursor] = useState<string | undefined>();
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
      <h1 className="font-display text-3xl">Bitácora</h1>
      <form className="mt-4 flex flex-wrap gap-2" onSubmit={(event) => { event.preventDefault(); setCursor(undefined); void logs.refetch(); }}>
        <input value={actor} onChange={(event) => setActor(event.target.value)} placeholder="Id del actor" aria-label="Actor" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input value={action} onChange={(event) => setAction(event.target.value)} placeholder="Acción" aria-label="Acción" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input value={entity} onChange={(event) => setEntity(event.target.value)} placeholder="Entidad" aria-label="Entidad" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} aria-label="Desde" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input type="date" value={to} onChange={(event) => setTo(event.target.value)} aria-label="Hasta" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <button type="submit" className="rounded bg-app-primary px-3 py-1 font-semibold text-app-on">Filtrar</button>
      </form>
      <PanelState loading={logs.isLoading} error={logs.error} empty={(logs.data?.items.length ?? 0) === 0}>
        <ul className="mt-4 grid gap-3">
          {logs.data?.items.map((item) => (
            <li key={item.id} className="rounded-xl border border-app-border bg-app-surface p-3 text-sm">
              <p className="font-semibold">{item.action} · {item.entityType}</p>
              <p className="break-all text-app-muted">{new Date(item.occurredAt).toLocaleString('es-CO')} · actor {item.actorUserId ?? 'sistema'} · {item.ipAddress ?? 'sin IP'}</p>
              {item.changes ? <pre className="mt-2 overflow-auto whitespace-pre-wrap font-mono text-xs">{item.changes}</pre> : null}
            </li>
          ))}
        </ul>
        {logs.data?.nextCursor ? <button type="button" className="mt-3" onClick={() => setCursor(logs.data?.nextCursor)}>Cargar más</button> : null}
      </PanelState>
      <h2 className="mt-8 font-semibold">Accesos a identidades anónimas</h2>
      <PanelState loading={access.isLoading} error={access.error} empty={(access.data?.items.length ?? 0) === 0}>
        <ul className="mt-2 grid gap-2 text-sm">
          {access.data?.items.map((item) => (
            <li key={item.id} className="rounded border border-app-border p-3">{new Date(item.createdAt).toLocaleString('es-CO')} · {item.targetType} · {item.justification}</li>
          ))}
        </ul>
        {access.data?.nextCursor ? <button type="button" className="mt-3" onClick={() => setAccessCursor(access.data?.nextCursor)}>Cargar más</button> : null}
      </PanelState>
    </section>
  );
}
