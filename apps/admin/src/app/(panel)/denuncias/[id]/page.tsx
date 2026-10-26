'use client';

import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { useState } from 'react';

type Context = {
  id: string;
  reason: string;
  status: string;
  isPriority: boolean;
  details?: string;
  target: { type: string; id: string; text?: string; status?: string; stillThereCount?: number; goneNowCount?: number; commentCount?: number; authorHidden: boolean; author?: { id: string; userName: string }; media: { id: string; type: string; status: string }[] };
  otherComplaints: { id: string; reason: string; status: string }[];
  sanctions: { id: string; action: string; reason: string }[];
};

export default function DenunciaPage() {
  const { id } = useParams<{ id: string }>();
  const me = useMe();
  const client = useQueryClient();
  const [action, setAction] = useState<'review' | 'uphold' | 'dismiss' | 'reveal' | null>(null);
  const [revealed, setRevealed] = useState('');
  const query = useQuery({ queryKey: ['complaint', id], queryFn: () => apiGet<Context>(`admin/complaints/${id}`), enabled: can(me.permissions, 'complaints.resolve') });
  if (!can(me.permissions, 'complaints.resolve')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  const data = query.data;
  async function confirm(reason: string) {
    if (action === 'review') {
      await apiSend(`moderation/complaints/${id}/start-review`, 'POST');
    } else if (action === 'uphold') {
      await apiSend(`moderation/complaints/${id}/uphold`, 'POST', { resolution: reason, action: 'Remove' });
    } else if (action === 'dismiss') {
      await apiSend(`moderation/complaints/${id}/dismiss`, 'POST', { resolution: reason });
    } else if (action === 'reveal' && data) {
      const result = await apiSend<{ userName: string }>('moderation/anonymous-identity/reveal', 'POST', {
        targetType: data.target.type,
        targetId: data.target.id,
        complaintId: data.id,
        justification: reason,
      });
      setRevealed(result.userName);
    }
    setAction(null);
    await client.invalidateQueries({ queryKey: ['complaint', id] });
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Denuncia</h1>
      <PanelState loading={query.isLoading} error={query.error}>
        {data ? (
          <div className="mt-4 grid gap-4">
            <p>{data.reason} · {data.status}{data.isPriority ? ' · prioritaria' : ''}</p>
            {data.details ? <p>{data.details}</p> : null}
            <article className="rounded-xl border border-app-border bg-app-surface p-4">
              <h2 className="font-semibold">Contenido</h2>
              <p className="mt-2">{data.target.text || 'Sin texto'}</p>
              <p className="text-sm text-app-muted">Estado {data.target.status ?? '—'} · sigue ahí {data.target.stillThereCount ?? 0} · ya no está {data.target.goneNowCount ?? 0} · comentarios {data.target.commentCount ?? 0}</p>
              <ul className="mt-2 text-sm">{data.target.media.map((media) => <li key={media.id}>{media.type} · {media.status}</li>)}</ul>
              {data.target.authorHidden ? <p className="mt-2">Autor oculto: el contenido es anónimo.</p> : <p className="mt-2">Autor: {data.target.author?.userName}</p>}
              {revealed ? <p>Identidad revelada para esta revisión: {revealed}</p> : null}
            </article>
            <section>
              <h2 className="font-semibold">Otras denuncias</h2>
              {data.otherComplaints.length === 0 ? <p className="text-app-muted">No hay otras denuncias sobre este contenido.</p> : <ul>{data.otherComplaints.map((item) => <li key={item.id}>{item.reason} · {item.status}</li>)}</ul>}
            </section>
            <section>
              <h2 className="font-semibold">Sanciones</h2>
              {data.sanctions.length === 0 ? <p className="text-app-muted">Sin sanciones visibles.</p> : <ul>{data.sanctions.map((item) => <li key={item.id}>{item.action}: {item.reason}</li>)}</ul>}
            </section>
            <div className="flex flex-wrap gap-2">
              <button type="button" className="rounded border border-app-border px-3 py-2" onClick={() => setAction('review')}>Iniciar revisión</button>
              <button type="button" className="rounded bg-app-primary px-3 py-2 font-semibold text-app-on" onClick={() => setAction('uphold')}>Mantener y quitar</button>
              <button type="button" className="rounded border border-app-border px-3 py-2" onClick={() => setAction('dismiss')}>Descartar</button>
              {data.target.authorHidden && can(me.permissions, 'anonymity.reveal') ? <button type="button" className="rounded border border-app-danger px-3 py-2 text-app-danger" onClick={() => setAction('reveal')}>Revelar identidad</button> : null}
            </div>
          </div>
        ) : null}
      </PanelState>
      {action ? (
        <ReasonDialog
          title={action === 'reveal' ? 'Revelar identidad' : 'Confirmar decisión'}
          confirmLabel="Confirmar"
          danger={action === 'uphold' || action === 'reveal'}
          extra={action === 'reveal' ? <p className="mt-3 text-sm">Esta consulta queda en la bitácora. La ley de habeas data exige una justificación concreta. No compartas la identidad fuera del caso.</p> : null}
          onClose={() => setAction(null)}
          onConfirm={confirm}
        />
      ) : null}
    </section>
  );
}
