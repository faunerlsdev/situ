'use client';

import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

type Page = { items: { id: string; trackingCode: string; status: string; daysRemaining: number; overdue: boolean; targetType: string; targetId: string }[]; nextCursor?: string };

export default function RetirosPage() {
  const me = useMe();
  const client = useQueryClient();
  const [cursor, setCursor] = useState<string | undefined>();
  const [chosen, setChosen] = useState<{ id: string; action: 'accept' | 'reject' } | null>(null);
  const query = useQuery({
    queryKey: ['takedowns', cursor],
    queryFn: () => apiGet<Page>(`admin/takedowns?limit=20${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`),
    enabled: can(me.permissions, 'takedowns.handle'),
  });
  if (!can(me.permissions, 'takedowns.handle')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Retiros</h1>
      <PanelState loading={query.isLoading} error={query.error} empty={(query.data?.items.length ?? 0) === 0}>
        <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[40rem] text-left text-sm">
          <thead><tr className="text-app-muted"><th className="py-2">Seguimiento</th><th>Estado</th><th>Plazo</th><th /></tr></thead>
          <tbody>
            {query.data?.items.map((item) => (
              <tr key={item.id} className="border-t border-app-border">
                <td className="py-2">{item.trackingCode}<div className="text-app-muted">{item.targetType}</div></td>
                <td>{item.status}</td>
                <td className={item.overdue ? 'text-app-danger' : ''}>{item.overdue ? 'Vencido' : `${item.daysRemaining} días`}</td>
                <td className="flex gap-2 py-2">
                  <button type="button" onClick={() => void apiSend(`admin/takedowns/${item.id}/start-review`, 'POST').then(() => client.invalidateQueries({ queryKey: ['takedowns'] }))}>Revisar</button>
                  <button type="button" onClick={() => setChosen({ id: item.id, action: 'accept' })}>Aceptar</button>
                  <button type="button" onClick={() => setChosen({ id: item.id, action: 'reject' })}>Rechazar</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
        {query.data?.nextCursor ? <button type="button" className="mt-4" onClick={() => setCursor(query.data?.nextCursor)}>Cargar más</button> : null}
      </PanelState>
      {chosen ? (
        <ReasonDialog title={chosen.action === 'accept' ? 'Aceptar retiro' : 'Rechazar retiro'} confirmLabel="Confirmar" danger={chosen.action === 'accept'} onClose={() => setChosen(null)} onConfirm={async (reason) => {
          await apiSend(`admin/takedowns/${chosen.id}/${chosen.action}`, 'POST', { resolution: reason });
          setChosen(null);
          await client.invalidateQueries({ queryKey: ['takedowns'] });
        }} />
      ) : null}
    </section>
  );
}
