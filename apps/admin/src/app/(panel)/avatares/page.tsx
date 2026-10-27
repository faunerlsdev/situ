'use client';

import { DataView, EyeIcon, IconButton, Modal, PageHeader, PencilIcon, PlusIcon, PrimaryButton, SearchField, StatusPill, fieldClass } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

type Avatar = { id: string; name: string; imageUrl: string; sortOrder: number; isActive: boolean };
type Gallery = { items: Avatar[] };

function imageSrc(url: string) {
  return url.startsWith('/uploads/') ? `/api${url}` : url;
}

export default function AvataresPage() {
  const me = useMe();
  const client = useQueryClient();
  const [query, setQuery] = useState('');
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<Avatar | null>(null);
  const [editing, setEditing] = useState<Avatar | null>(null);
  const [message, setMessage] = useState('');
  const list = useQuery({ queryKey: ['avatars'], queryFn: () => apiGet<Gallery>('admin/avatars'), enabled: can(me.permissions, 'avatars.manage') });
  const items = list.data?.items ?? [];
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return items;
    return items.filter((item) => `${item.name} ${item.isActive ? 'activo' : 'inactivo'}`.toLowerCase().includes(needle));
  }, [items, query]);

  if (!can(me.permissions, 'avatars.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  async function refresh() {
    await client.invalidateQueries({ queryKey: ['avatars'] });
  }

  return (
    <section>
      <PageHeader title="Avatares" hint="Imágenes que las personas eligen para su perfil." action={
        <PrimaryButton onClick={() => { setMessage(''); setCreating(true); }}><PlusIcon /> Nuevo</PrimaryButton>
      } />
      <PanelState loading={list.isLoading} error={list.error}>
        <div className="mt-6 grid gap-4">
          <SearchField value={query} onChange={setQuery} placeholder="Filtrar por nombre" />
          <DataView
            columns={['Nombre', 'Orden', 'Estado']}
            emptyLabel={items.length === 0 ? 'Todavía no hay avatares.' : 'Ningún avatar coincide con el filtro.'}
            rows={filtered.map((item) => ({
              id: item.id,
              cells: [
                <span key="name" className="flex items-center gap-3 font-semibold">
                  <img src={imageSrc(item.imageUrl)} alt="" width={36} height={36} className="h-9 w-9 rounded-lg object-cover" />
                  {item.name}
                </span>,
                item.sortOrder,
                <StatusPill key="state" on={item.isActive} onLabel="Activo" offLabel="Inactivo" />,
              ],
              actions: (
                <>
                  <IconButton label={`Ver ${item.name}`} onClick={() => setViewing(item)}><EyeIcon /></IconButton>
                  <IconButton label={`Editar ${item.name}`} onClick={() => { setMessage(''); setEditing(item); }}><PencilIcon /></IconButton>
                </>
              ),
            }))}
          />
        </div>
      </PanelState>
      {creating ? (
        <Modal title="Nuevo avatar" onClose={() => setCreating(false)}>
          <AvatarForm message={message} onSubmit={async (data) => {
            try {
              await apiSend('admin/avatars', 'POST', data);
              setMessage('');
              setCreating(false);
              await refresh();
            } catch (cause) {
              setMessage(cause instanceof Error ? cause.message : 'No pudimos subir el avatar.');
            }
          }} />
        </Modal>
      ) : null}
      {viewing ? (
        <Modal title={viewing.name} onClose={() => setViewing(null)}>
          <img src={imageSrc(viewing.imageUrl)} alt="" width={160} height={160} className="h-40 w-40 rounded-2xl object-cover" />
          <dl className="mt-4 grid gap-3 sm:grid-cols-2">
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Orden</dt><dd>{viewing.sortOrder}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Estado</dt><dd>{viewing.isActive ? 'Activo' : 'Inactivo'}</dd></div>
          </dl>
        </Modal>
      ) : null}
      {editing ? (
        <Modal title={`Editar ${editing.name}`} onClose={() => setEditing(null)}>
          <AvatarForm
            avatar={editing}
            message={message}
            onSubmit={async (data) => {
              try {
                await apiSend(`admin/avatars/${editing.id}`, 'PUT', data);
                setMessage('');
                setEditing(null);
                await refresh();
              } catch (cause) {
                setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar el avatar.');
              }
            }}
          />
        </Modal>
      ) : null}
    </section>
  );
}

function AvatarForm({ avatar, message, onSubmit }: { avatar?: Avatar; message: string; onSubmit: (data: FormData) => Promise<void> }) {
  return (
    <form className="grid gap-3" onSubmit={(event) => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      const active = event.currentTarget.elements.namedItem('isActive');
      data.set('isActive', active instanceof HTMLInputElement && active.checked ? 'true' : 'false');
      void onSubmit(data);
    }}>
      <label className="text-sm">Nombre<input name="name" required defaultValue={avatar?.name} aria-label="Nombre" className={fieldClass} /></label>
      <label className="text-sm">Orden<input name="sortOrder" type="number" defaultValue={avatar?.sortOrder ?? 0} aria-label="Orden" className={fieldClass} /></label>
      <label className="flex items-center gap-2 text-sm"><input name="isActive" type="checkbox" defaultChecked={avatar?.isActive ?? true} /> Activo</label>
      <label className="text-sm">Imagen<input name="file" type="file" required={!avatar} accept="image/png,image/jpeg,image/webp" aria-label="Imagen" className="mt-1 block w-full text-sm" /></label>
      <div className="flex justify-end"><PrimaryButton type="submit">{avatar ? 'Guardar' : 'Subir'}</PrimaryButton></div>
      {message ? <p role="alert" className="text-sm text-app-danger">{message}</p> : null}
    </form>
  );
}
