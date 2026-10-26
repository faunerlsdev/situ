'use client';

import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

type Holds = { holds: { id: string; targetType: string; targetId: string; reason: string; active: boolean; createdAt: string }[] };
type Policies = { policies: { kind: string; retainDays: number; isActive: boolean }[] };

export default function RetencionPage() {
  const me = useMe();
  const client = useQueryClient();
  const [release, setRelease] = useState<string | null>(null);
  const [message, setMessage] = useState('');
  const holds = useQuery({ queryKey: ['holds'], queryFn: () => apiGet<Holds>('admin/legal-holds?activeOnly=true'), enabled: can(me.permissions, 'legalholds.manage') });
  const policies = useQuery({ queryKey: ['retention'], queryFn: () => apiGet<Policies>('admin/retention-policies'), enabled: can(me.permissions, 'legalholds.manage') });
  if (!can(me.permissions, 'legalholds.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Retención y bloqueos legales</h1>
      {message ? <p role="status" className="mt-2">{message}</p> : null}
      <PanelState loading={holds.isLoading} error={holds.error} empty={(holds.data?.holds.length ?? 0) === 0}>
        <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[36rem] text-left text-sm">
          <thead><tr className="text-app-muted"><th className="py-2">Objetivo</th><th>Motivo</th><th /></tr></thead>
          <tbody>
            {holds.data?.holds.map((hold) => (
              <tr key={hold.id} className="border-t border-app-border">
                <td className="py-2">{hold.targetType} · {hold.targetId}</td>
                <td>{hold.reason}</td>
                <td>{hold.active ? <button type="button" onClick={() => setRelease(hold.id)}>Liberar</button> : 'Liberado'}</td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
      </PanelState>
      <form className="mt-6 grid max-w-lg gap-2" onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        void apiSend('admin/legal-holds', 'POST', { targetType: data.get('type'), targetId: data.get('target'), reason: data.get('reason') })
          .then(() => { setMessage('Bloqueo creado.'); event.currentTarget.reset(); return client.invalidateQueries({ queryKey: ['holds'] }); })
          .catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos crear el bloqueo.'));
      }}>
        <h2 className="font-semibold">Nuevo bloqueo</h2>
        <select name="type" aria-label="Tipo" className="rounded border border-app-border bg-app-input px-3 py-2"><option value="User">Usuario</option><option value="Report">Reporte</option><option value="Comment">Comentario</option></select>
        <input name="target" required placeholder="Id del objetivo" aria-label="Id del objetivo" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <textarea name="reason" required minLength={3} placeholder="Motivo" aria-label="Motivo" className="min-h-20 rounded border border-app-border bg-app-input p-3" />
        <button type="submit" className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Crear</button>
      </form>
      <section className="mt-8">
        <h2 className="font-semibold">Política de retención</h2>
        <PanelState loading={policies.isLoading} error={policies.error}>
          <ul className="mt-2 grid gap-2">
            {policies.data?.policies.map((policy) => (
              <li key={policy.kind}>
                <form className="flex flex-wrap items-center gap-2" onSubmit={(event) => {
                  event.preventDefault();
                  const days = Number(new FormData(event.currentTarget).get('days'));
                  void apiSend(`admin/retention-policies/${policy.kind}`, 'PUT', { retainDays: days }).then(() => setMessage('Política actualizada.')).catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.'));
                }}>
                  <span className="min-w-0 sm:w-48">{policy.kind}</span>
                  <input name="days" type="number" min={1} defaultValue={policy.retainDays} aria-label={`Días de ${policy.kind}`} className="w-24 rounded border border-app-border bg-app-input px-2 py-1" />
                  <span className="text-sm text-app-muted">{policy.isActive ? 'activa' : 'inactiva'}</span>
                  <button type="submit" className="rounded border border-app-border px-3 py-1">Guardar</button>
                </form>
              </li>
            ))}
          </ul>
        </PanelState>
      </section>
      {release ? (
        <ReasonDialog title="Liberar bloqueo legal" confirmLabel="Liberar" danger onClose={() => setRelease(null)} onConfirm={async (reason) => {
          await apiSend(`admin/legal-holds/${release}/release`, 'POST', { resolution: reason });
          setRelease(null);
          setMessage('Bloqueo liberado.');
          await client.invalidateQueries({ queryKey: ['holds'] });
        }} />
      ) : null}
    </section>
  );
}
