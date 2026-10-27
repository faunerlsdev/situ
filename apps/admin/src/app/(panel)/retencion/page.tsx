'use client';

import { DataView, EyeIcon, IconButton, Modal, PageHeader, PencilIcon, PlusIcon, PrimaryButton, SearchField, fieldClass } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

type Hold = { id: string; targetType: string; targetId: string; reason: string; active: boolean; createdAt: string };
type Holds = { holds: Hold[] };
type Policy = { kind: string; retainDays: number; isActive: boolean };
type Policies = { policies: Policy[] };

export default function RetencionPage() {
  const me = useMe();
  const client = useQueryClient();
  const [query, setQuery] = useState('');
  const [release, setRelease] = useState<string | null>(null);
  const [message, setMessage] = useState('');
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<Hold | null>(null);
  const [editing, setEditing] = useState<Policy | null>(null);
  const holds = useQuery({ queryKey: ['holds'], queryFn: () => apiGet<Holds>('admin/legal-holds?activeOnly=true'), enabled: can(me.permissions, 'legalholds.manage') });
  const policies = useQuery({ queryKey: ['retention'], queryFn: () => apiGet<Policies>('admin/retention-policies'), enabled: can(me.permissions, 'legalholds.manage') });
  const rows = holds.data?.holds ?? [];
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter((hold) => `${hold.targetType} ${hold.targetId} ${hold.reason}`.toLowerCase().includes(needle));
  }, [rows, query]);

  if (!can(me.permissions, 'legalholds.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <PageHeader title="Retención y bloqueos legales" hint="Bloqueos activos y cuánto se conserva cada tipo de dato." action={
        <PrimaryButton onClick={() => { setMessage(''); setCreating(true); }}><PlusIcon /> Nuevo</PrimaryButton>
      } />
      {message ? <p role="status" className="mt-3 text-sm">{message}</p> : null}
      <PanelState loading={holds.isLoading} error={holds.error}>
        <div className="mt-6 grid gap-4">
          <SearchField value={query} onChange={setQuery} placeholder="Filtrar por objetivo o motivo" />
          <DataView
            columns={['Objetivo', 'Motivo', 'Desde']}
            emptyLabel={rows.length === 0 ? 'No hay bloqueos activos.' : 'Ningún bloqueo coincide con el filtro.'}
            rows={filtered.map((hold) => ({
              id: hold.id,
              cells: [
                <span key="target" className="font-semibold">{hold.targetType}</span>,
                <span key="reason" className="line-clamp-2">{hold.reason}</span>,
                new Date(hold.createdAt).toLocaleString('es-CO'),
              ],
              actions: <IconButton label="Ver bloqueo" onClick={() => setViewing(hold)}><EyeIcon /></IconButton>,
            }))}
          />
        </div>
      </PanelState>
      <h2 className="mt-10 font-display text-xl">Política de retención</h2>
      <PanelState loading={policies.isLoading} error={policies.error}>
        <div className="mt-4">
          <DataView
            columns={['Tipo', 'Días', 'Estado']}
            emptyLabel="No hay políticas."
            rows={(policies.data?.policies ?? []).map((policy) => ({
              id: policy.kind,
              cells: [policy.kind, policy.retainDays, policy.isActive ? 'Activa' : 'Inactiva'],
              actions: <IconButton label={`Editar ${policy.kind}`} onClick={() => setEditing(policy)}><PencilIcon /></IconButton>,
            }))}
          />
        </div>
      </PanelState>
      {creating ? (
        <Modal title="Nuevo bloqueo" onClose={() => setCreating(false)}>
          <form className="grid gap-3" onSubmit={(event) => {
            event.preventDefault();
            const data = new FormData(event.currentTarget);
            void apiSend('admin/legal-holds', 'POST', { targetType: data.get('type'), targetId: data.get('target'), reason: data.get('reason') })
              .then(async () => { setMessage('Bloqueo creado.'); setCreating(false); await client.invalidateQueries({ queryKey: ['holds'] }); })
              .catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos crear el bloqueo.'));
          }}>
            <label className="text-sm">Tipo
              <select name="type" aria-label="Tipo" className={fieldClass}><option value="User">Usuario</option><option value="Report">Reporte</option><option value="Comment">Comentario</option></select>
            </label>
            <label className="text-sm">Id del objetivo<input name="target" required aria-label="Id del objetivo" className={fieldClass} /></label>
            <label className="text-sm">Motivo<textarea name="reason" required minLength={3} aria-label="Motivo" className={`${fieldClass} min-h-24`} /></label>
            <div className="flex justify-end"><PrimaryButton type="submit">Crear</PrimaryButton></div>
          </form>
        </Modal>
      ) : null}
      {viewing ? (
        <Modal title="Bloqueo legal" onClose={() => setViewing(null)}>
          <dl className="grid gap-3 sm:grid-cols-2">
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Tipo</dt><dd>{viewing.targetType}</dd></div>
            <div className="break-all"><dt className="text-xs tracking-wide text-app-muted uppercase">Objetivo</dt><dd>{viewing.targetId}</dd></div>
            <div className="sm:col-span-2"><dt className="text-xs tracking-wide text-app-muted uppercase">Motivo</dt><dd>{viewing.reason}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Desde</dt><dd>{new Date(viewing.createdAt).toLocaleString('es-CO')}</dd></div>
          </dl>
          {viewing.active ? <button type="button" className="mt-4 rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setRelease(viewing.id)}>Liberar</button> : null}
        </Modal>
      ) : null}
      {editing ? (
        <Modal title={`Editar ${editing.kind}`} onClose={() => setEditing(null)}>
          <form className="grid gap-3" onSubmit={(event) => {
            event.preventDefault();
            const days = Number(new FormData(event.currentTarget).get('days'));
            void apiSend(`admin/retention-policies/${editing.kind}`, 'PUT', { retainDays: days })
              .then(async () => { setMessage('Política actualizada.'); setEditing(null); await client.invalidateQueries({ queryKey: ['retention'] }); })
              .catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.'));
          }}>
            <label className="text-sm">Días de conservación<input name="days" type="number" min={1} required defaultValue={editing.retainDays} aria-label={`Días de ${editing.kind}`} className={fieldClass} /></label>
            <div className="flex justify-end"><PrimaryButton type="submit">Guardar</PrimaryButton></div>
          </form>
        </Modal>
      ) : null}
      {release ? (
        <ReasonDialog title="Liberar bloqueo legal" confirmLabel="Liberar" danger onClose={() => setRelease(null)} onConfirm={async (reason) => {
          await apiSend(`admin/legal-holds/${release}/release`, 'POST', { resolution: reason });
          setRelease(null);
          setViewing(null);
          setMessage('Bloqueo liberado.');
          await client.invalidateQueries({ queryKey: ['holds'] });
        }} />
      ) : null}
    </section>
  );
}
