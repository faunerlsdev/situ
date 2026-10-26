'use client';

import { useMe } from '@/components/frame';
import { EmailReveal } from '@/components/email-reveal';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { useState } from 'react';

type Detail = {
  id: string;
  userName: string;
  email: string;
  status: string;
  isMinor: boolean;
  memberSinceYear: number;
  memberSinceMonth: number;
  planCode: string;
  deviceCount: number;
  reportCount: number;
  complaintsReceived: number;
  sanctionCount: number;
};

export default function UsuarioPage() {
  const { id } = useParams<{ id: string }>();
  const me = useMe();
  const [reveal, setReveal] = useState(false);
  const [action, setAction] = useState<string | null>(null);
  const [until, setUntil] = useState('');
  const query = useQuery({
    queryKey: ['user', id, reveal],
    queryFn: () => apiGet<Detail>(`admin/users/${id}${reveal ? '?revealEmail=true' : ''}`),
    enabled: can(me.permissions, 'users.view'),
  });
  if (!can(me.permissions, 'users.view')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  const user = query.data;
  return (
    <section>
      <h1 className="font-display text-3xl">Ficha</h1>
      <PanelState loading={query.isLoading} error={query.error}>
        {user ? (
          <div className="mt-4 grid gap-2">
            <p className="font-semibold">{user.userName}</p>
            <EmailReveal email={user.email} canReveal={!reveal && can(me.permissions, 'users.view_email')} onReveal={() => setReveal(true)} />
            {reveal ? <p className="text-sm text-app-muted">Esta consulta queda registrada en la bitácora.</p> : null}
            <p>Estado {user.status}{user.isMinor ? ' · menor de edad' : ''} · plan {user.planCode}</p>
            <p>Miembro desde {user.memberSinceMonth}/{user.memberSinceYear}</p>
            <p>Dispositivos {user.deviceCount} · reportes públicos {user.reportCount} · denuncias {user.complaintsReceived} · sanciones {user.sanctionCount}</p>
            {can(me.permissions, 'users.suspend') ? (
              <div className="mt-4 flex flex-wrap gap-2">
                {['warn', 'suspend', 'ban', 'reinstate', 'force-logout'].map((item) => (
                  <button key={item} type="button" className="rounded border border-app-border px-3 py-2" onClick={() => setAction(item)}>
                    {label(item)}
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        ) : null}
      </PanelState>
      {action ? (
        <ReasonDialog
          title={label(action)}
          confirmLabel="Confirmar"
          danger={action === 'ban' || action === 'suspend'}
          extra={action === 'suspend' ? <label className="mt-3 block">Hasta<input type="datetime-local" required value={until} onChange={(event) => setUntil(event.target.value)} className="mt-1 w-full rounded border border-app-border bg-app-input px-3 py-2" /></label> : null}
          onClose={() => setAction(null)}
          onConfirm={async (reason) => {
            await apiSend(`admin/users/${id}/${action}`, 'POST', { reason, until: action === 'suspend' ? new Date(until).toISOString() : undefined });
            setAction(null);
            await query.refetch();
          }}
        />
      ) : null}
    </section>
  );
}

function label(action: string) {
  return { warn: 'Advertir', suspend: 'Suspender', ban: 'Bloquear', reinstate: 'Reinstalar', 'force-logout': 'Cerrar sesiones' }[action] ?? action;
}
