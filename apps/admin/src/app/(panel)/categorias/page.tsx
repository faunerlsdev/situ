'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

type Category = {
  id: string;
  code: string;
  name: string;
  description: string;
  buttonIconUrl: string;
  mapIconUrl: string;
  colorHex: string;
  sortOrder: number;
  isActive: boolean;
  ttlMinutes: number;
  importanceWeight: number;
  mediaRequirement: string;
  isAdultOnly: boolean;
  translations: { locale: string; name: string; description: string }[];
  overrides: { countryId?: string; cityId?: string; isEnabled: boolean; ttlMinutes?: number }[];
};
type Catalog = { categories: Category[]; catalogVersion: number };
const emptyActor = '00000000-0000-0000-0000-000000000000';

function preview(url: string) {
  return url.startsWith('/uploads/') ? `/api${url}` : url;
}

export default function CategoriasPage() {
  const me = useMe();
  const client = useQueryClient();
  const [message, setMessage] = useState('');
  const query = useQuery({ queryKey: ['categories'], queryFn: () => apiGet<Catalog>('admin/categories'), enabled: can(me.permissions, 'categories.manage') });
  if (!can(me.permissions, 'categories.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  async function create(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await apiSend('admin/categories', 'POST', {
        actorId: emptyActor,
        code: data.get('code'),
        name: data.get('name'),
        description: data.get('description'),
        buttonIconUrl: '/uploads/categories/pending/button.png',
        mapIconUrl: '/uploads/categories/pending/map.png',
        colorHex: data.get('color'),
        sortOrder: Number(data.get('order') ?? 0),
        ttlMinutes: Number(data.get('ttl') ?? 60),
        importanceWeight: Number(data.get('weight') ?? 1),
        mediaRequirement: data.get('media'),
        isAdultOnly: data.get('adult') === 'on',
      });
      setMessage('');
      await client.invalidateQueries({ queryKey: ['categories'] });
      event.currentTarget.reset();
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.');
    }
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Categorías</h1>
      <PanelState loading={query.isLoading} error={query.error} empty={(query.data?.categories.length ?? 0) === 0}>
        <p className="mt-2 text-sm text-app-muted">Versión del catálogo: {query.data?.catalogVersion}</p>
        <ul className="mt-4 grid gap-4">
          {query.data?.categories.map((category) => <CategoryRow key={category.id} category={category} ids={query.data?.categories.map((item) => item.id) ?? []} onChange={() => client.invalidateQueries({ queryKey: ['categories'] })} />)}
        </ul>
      </PanelState>
      <form className="mt-8 grid max-w-xl gap-2" onSubmit={create}>
        <h2 className="font-semibold">Nueva categoría</h2>
        <input name="code" required placeholder="Código" aria-label="Código" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <input name="name" required placeholder="Nombre" aria-label="Nombre" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <input name="description" required placeholder="Descripción" aria-label="Descripción" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <input name="color" required defaultValue="#FF6400" aria-label="Color" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <input name="ttl" type="number" min={5} defaultValue={60} aria-label="Minutos de vida" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <input name="weight" type="number" min={0} max={10} step="0.1" defaultValue={1} aria-label="Peso" className="rounded border border-app-border bg-app-input px-3 py-2" />
        <select name="media" aria-label="Medio requerido" className="rounded border border-app-border bg-app-input px-3 py-2"><option value="None">Sin medio</option><option value="PhotoRequired">Foto</option><option value="VideoRequired">Video</option><option value="PhotoOrVideoRequired">Foto o video</option></select>
        <label className="flex gap-2"><input name="adult" type="checkbox" /> Solo adultos</label>
        <button type="submit" className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Crear</button>
        {message ? <p role="alert" className="text-app-danger">{message}</p> : null}
      </form>
    </section>
  );
}

function CategoryRow({ category, ids, onChange }: { category: Category; ids: string[]; onChange: () => Promise<void> }) {
  const [message, setMessage] = useState('');
  async function run(task: () => Promise<unknown>) {
    try {
      await task();
      setMessage('');
      await onChange();
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.');
    }
  }

  return (
    <li className="rounded-xl border border-app-border bg-app-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="font-semibold">{category.name} · {category.code}</p>
          <p className="text-sm text-app-muted">{category.isActive ? 'Activa' : 'Inactiva'} · TTL {category.ttlMinutes} min</p>
          <p className="text-sm text-app-muted">Traducciones: {category.translations.map((item) => item.locale).join(', ') || 'ninguna'} · Excepciones: {category.overrides.length}</p>
        </div>
        <div className="flex gap-2">
          {category.buttonIconUrl ? <img src={preview(category.buttonIconUrl)} alt="Ícono del botón" width={32} height={32} /> : null}
          {category.mapIconUrl ? <img src={preview(category.mapIconUrl)} alt="Ícono del mapa" width={32} height={32} /> : null}
        </div>
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" className="rounded border border-app-border px-3 py-1" onClick={() => run(() => apiSend(`admin/categories/${category.id}/active`, 'POST', { isActive: !category.isActive }))}>{category.isActive ? 'Desactivar' : 'Activar'}</button>
        <button type="button" className="rounded border border-app-border px-3 py-1" onClick={() => run(() => apiSend('admin/categories/order', 'PUT', { categoryIds: [category.id, ...ids.filter((item) => item !== category.id)] }))}>Subir al inicio</button>
      </div>
      <form className="mt-3 flex flex-wrap gap-2" onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        void run(() => apiSend(`admin/categories/${category.id}/translations`, 'PUT', { actorId: emptyActor, categoryId: category.id, locale: data.get('locale'), name: data.get('name'), description: data.get('description') }));
      }}>
        <input name="locale" required placeholder="es-CO" aria-label="Idioma" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input name="name" required placeholder="Nombre traducido" aria-label="Nombre traducido" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input name="description" required placeholder="Descripción" aria-label="Descripción traducida" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <button type="submit" className="rounded border border-app-border px-3 py-1">Traducción</button>
      </form>
      <form className="mt-2 flex flex-wrap gap-2" onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        void run(() => apiSend(`admin/categories/${category.id}/overrides`, 'PUT', { actorId: emptyActor, categoryId: category.id, countryId: data.get('country') || null, cityId: data.get('city') || null, isEnabled: data.get('enabled') === 'on', ttlMinutes: data.get('ttl') ? Number(data.get('ttl')) : null }));
      }}>
        <input name="country" placeholder="Id del país" aria-label="País" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input name="city" placeholder="Id de la ciudad" aria-label="Ciudad" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input name="ttl" type="number" placeholder="TTL" aria-label="TTL de la excepción" className="w-24 rounded border border-app-border bg-app-input px-2 py-1" />
        <label className="flex items-center gap-1"><input name="enabled" type="checkbox" defaultChecked /> Activa</label>
        <button type="submit" className="rounded border border-app-border px-3 py-1">Excepción</button>
      </form>
      <form className="mt-2 flex flex-wrap items-end gap-2" onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        void run(async () => {
          await apiSend(`admin/categories/${category.id}`, 'PUT', {
            actorId: emptyActor,
            categoryId: category.id,
            name: String(data.get('name') || category.name),
            description: String(data.get('description') || category.description),
            buttonIconUrl: category.buttonIconUrl,
            mapIconUrl: category.mapIconUrl,
            colorHex: category.colorHex,
            sortOrder: category.sortOrder,
            ttlMinutes: category.ttlMinutes,
            importanceWeight: category.importanceWeight,
            mediaRequirement: category.mediaRequirement,
            isAdultOnly: category.isAdultOnly,
          });
          const button = data.get('button');
          const map = data.get('map');
          if (button instanceof File && button.size > 0 || map instanceof File && map.size > 0) {
            const files = new FormData();
            if (button instanceof File && button.size > 0) files.set('button', button);
            if (map instanceof File && map.size > 0) files.set('map', map);
            await apiSend(`admin/categories/${category.id}/icons`, 'POST', files);
          }
        });
      }}>
        <input name="name" defaultValue={category.name} aria-label="Editar nombre" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <input name="description" defaultValue={category.description} aria-label="Editar descripción" className="rounded border border-app-border bg-app-input px-2 py-1" />
        <label className="text-sm">Ícono del botón<input name="button" type="file" accept="image/png,image/jpeg,image/webp" className="block" /></label>
        <label className="text-sm">Ícono del mapa<input name="map" type="file" accept="image/png,image/jpeg,image/webp" className="block" /></label>
        <button type="submit" className="rounded bg-app-primary px-3 py-1 font-semibold text-app-on">Guardar</button>
      </form>
      {message ? <p role="alert" className="mt-2 text-app-danger">{message}</p> : null}
    </li>
  );
}
