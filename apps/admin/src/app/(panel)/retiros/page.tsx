'use client';

import { DataView, EyeIcon, IconButton, Modal, PageHeader, SearchField } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

type Item = { id: string; trackingCode: string; status: string; daysRemaining: number; overdue: boolean; targetType: string; targetId: string };
type Page = { items: Item[]; nextCursor?: string };

export default function RetirosPage() {
  const me = useMe();
  const client = useQueryClient();
  const [cursor, setCursor] = useState<string | undefined>();
  const [query, setQuery] = useState('');
  const [viewing, setViewing] = useState<Item | null>(null);
  const [chosen, setChosen] = useState<{ id: string; action: 'accept' | 'reject' } | null>(null);
  const list = useQuery({
    queryKey: ['takedowns', cursor],
    queryFn: () => apiGet<Page>(`admin/takedowns?limit=20${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`),
    enabled: can(me.permissions, 'takedowns.handle'),
  });
  const items = list.data?.items ?? [];
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return items;
    return items.filter((item) => `${item.trackingCode} ${item.status} ${item.targetType}`.toLowerCase().includes(needle));
  }, [items, query]);

  if (!can(me.permissions, 'takedowns.handle')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  async function refresh() {
    await client.invalidateQueries({ queryKey: ['takedowns'] });
  }

  return (
    <section>
      <PageHeader title="Retiros" hint="Solicitudes de retiro de contenido." />
      <PanelState loading={list.isLoading} error={list.error}>
        <div className="mt-6 grid gap-4">
          <SearchField value={query} onChange={setQuery} placeholder="Filtrar por seguimiento o estado" />
          <DataView
            columns={['Seguimiento', 'Estado', 'Plazo']}
            emptyLabel={items.length === 0 ? 'No hay retiros en esta página.' : 'Ningún retiro coincide con el filtro.'}
            rows={filtered.map((item) => ({
              id: item.id,
              cells: [
                <span key="code"><span className="font-semibold">{item.trackingCode}</span><span className="block text-xs text-app-muted">{item.targetType}</span></span>,
                item.status,
                <span key="due" className={item.overdue ? 'text-app-danger' : ''}>{item.overdue ? 'Vencido' : `${item.daysRemaining} días`}</span>,
              ],
              actions: <IconButton label={`Ver ${item.trackingCode}`} onClick={() => setViewing(item)}><EyeIcon /></IconButton>,
            }))}
          />
          {list.data?.nextCursor ? <button type="button" className="justify-self-start rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setCursor(list.data?.nextCursor)}>Cargar más</button> : null}
        </div>
      </PanelState>
      {viewing ? (
        <Modal title={viewing.trackingCode} onClose={() => setViewing(null)}>
          <dl className="grid gap-3 sm:grid-cols-2">
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Estado</dt><dd>{viewing.status}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Plazo</dt><dd className={viewing.overdue ? 'text-app-danger' : ''}>{viewing.overdue ? 'Vencido' : `${viewing.daysRemaining} días`}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Objetivo</dt><dd>{viewing.targetType}</dd></div>
            <div className="break-all"><dt className="text-xs tracking-wide text-app-muted uppercase">Id</dt><dd>{viewing.targetId}</dd></div>
          </dl>
          <div className="mt-4 flex flex-wrap gap-2">
            <button type="button" className="rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => void apiSend(`admin/takedowns/${viewing.id}/start-review`, 'POST').then(refresh)}>Revisar</button>
            <button type="button" className="rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setChosen({ id: viewing.id, action: 'accept' })}>Aceptar</button>
            <button type="button" className="rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setChosen({ id: viewing.id, action: 'reject' })}>Rechazar</button>
          </div>
        </Modal>
      ) : null}
      {chosen ? (
        <ReasonDialog title={chosen.action === 'accept' ? 'Aceptar retiro' : 'Rechazar retiro'} confirmLabel="Confirmar" danger={chosen.action === 'accept'} onClose={() => setChosen(null)} onConfirm={async (reason) => {
          await apiSend(`admin/takedowns/${chosen.id}/${chosen.action}`, 'POST', { resolution: reason });
          setChosen(null);
          setViewing(null);
          await refresh();
        }} />
      ) : null}
    </section>
  );
}
