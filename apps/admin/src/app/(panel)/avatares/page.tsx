'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

type Gallery = { items: { id: string; name: string; imageUrl: string; sortOrder: number; isActive: boolean }[] };

export default function AvataresPage() {
  const me = useMe();
  const client = useQueryClient();
  const [message, setMessage] = useState('');
  const query = useQuery({ queryKey: ['avatars'], queryFn: () => apiGet<Gallery>('admin/avatars'), enabled: can(me.permissions, 'avatars.manage') });
  if (!can(me.permissions, 'avatars.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  async function create(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const active = event.currentTarget.elements.namedItem('isActive');
    data.set('isActive', active instanceof HTMLInputElement && active.checked ? 'true' : 'false');
    try {
      await apiSend('admin/avatars', 'POST', data);
      setMessage('');
      event.currentTarget.reset();
      await client.invalidateQueries({ queryKey: ['avatars'] });
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos subir el avatar.');
    }
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Avatares</h1>
      <PanelState loading={query.isLoading} error={query.error} empty={(query.data?.items.length ?? 0) === 0}>
        <ul className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {query.data?.items.map((item) => (
            <li key={item.id} className="rounded-xl border border-app-border bg-app-surface p-3">
              <img src={item.imageUrl.startsWith('/uploads/') ? `/api${item.imageUrl}` : item.imageUrl} alt="" width={96} height={96} className="h-24 w-24 rounded object-cover" />
              <p className="mt-2 font-semibold">{item.name}</p>
              <p className="text-sm text-app-muted">Orden {item.sortOrder} · {item.isActive ? 'Activo' : 'Inactivo'}</p>
              <button type="button" className="mt-2 text-sm font-semibold text-app-primary" onClick={() => void apiSend(`admin/avatars/${item.id}/active`, 'POST', { isActive: !item.isActive }).then(() => client.invalidateQueries({ queryKey: ['avatars'] }))}>
                {item.isActive ? 'Desactivar' : 'Activar'}
              </button>
            </li>
          ))}
        </ul>
      </PanelState>
      <form className="mt-8 grid max-w-md gap-2" onSubmit={create}>
        <h2 className="font-semibold">Subir avatar</h2>
        <input name="name" required aria-label="Nombre" placeholder="Nombre" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <input name="sortOrder" type="number" defaultValue={0} aria-label="Orden" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <label className="flex gap-2"><input name="isActive" type="checkbox" defaultChecked /> Activo</label>
        <input name="file" type="file" required accept="image/png,image/jpeg,image/webp" aria-label="Imagen" />
        <button type="submit" className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Subir</button>
        {message ? <p role="alert" className="text-app-danger">{message}</p> : null}
      </form>
    </section>
  );
}
