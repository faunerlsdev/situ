'use client';

import { DataView, EyeIcon, IconButton, Modal, PencilIcon, PlusIcon, PrimaryButton, SearchField, StatusPill, fieldClass } from '@/components/catalog';
import { apiGet, apiSend } from '@/lib/client';
import { useEffect, useMemo, useState } from 'react';

export const minTtlMinutes = 5;
export const maxTtlMinutes = 60 * 24 * 30;

export type Category = {
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
  activeReportCount?: number;
  translations: { locale: string; name: string; description: string }[];
  overrides: { countryId?: string; cityId?: string; isEnabled: boolean; ttlMinutes?: number }[];
};

const emptyActor = '00000000-0000-0000-0000-000000000000';
const mediaLabels: Record<string, string> = {
  None: 'Sin medio',
  PhotoRequired: 'Foto',
  VideoRequired: 'Video',
  PhotoOrVideoRequired: 'Foto o video',
};

function preview(url: string) {
  return url.startsWith('/uploads/') ? `/api${url}` : url;
}

export function formatVigencia(ttlMinutes: number): string {
  if (!Number.isFinite(ttlMinutes) || ttlMinutes <= 0) {
    return '';
  }

  const total = Math.trunc(ttlMinutes);
  const days = Math.floor(total / (60 * 24));
  const hours = Math.floor((total % (60 * 24)) / 60);
  const minutes = total % 60;
  const parts: string[] = [];
  if (days === 1) parts.push('1 día');
  else if (days > 1) parts.push(`${days} días`);
  if (hours === 1) parts.push('1 hora');
  else if (hours > 1) parts.push(`${hours} horas`);
  if (minutes === 1) parts.push('1 minuto');
  else if (minutes > 1) parts.push(`${minutes} minutos`);
  if (parts.length <= 1) {
    return parts[0] ?? '';
  }

  return `${parts.slice(0, -1).join(', ')} y ${parts[parts.length - 1]}`;
}

export function categoryCodeFromName(name: string): string | null {
  const code = name
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 40)
    .replace(/_+$/g, '');

  return /^[a-z0-9_]{2,40}$/.test(code) ? code : null;
}

export function vigenciaError(ttlMinutes: number): string | null {
  if (!Number.isInteger(ttlMinutes) || ttlMinutes < minTtlMinutes || ttlMinutes > maxTtlMinutes) {
    return 'La vigencia debe estar entre 5 minutos y 30 días.';
  }

  return null;
}

export function CategoryList({ categories, onChange }: { categories: Category[]; onChange: () => Promise<void> }) {
  const [query, setQuery] = useState('');
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<Category | null>(null);
  const [editing, setEditing] = useState<Category | null>(null);
  const [message, setMessage] = useState('');
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) {
      return categories;
    }

    return categories.filter((category) => {
      const vigencia = formatVigencia(category.ttlMinutes);
      return [category.name, category.code, vigencia, category.isActive ? 'activa' : 'inactiva'].join(' ').toLowerCase().includes(needle);
    });
  }, [categories, query]);

  async function run(task: () => Promise<unknown>) {
    try {
      await task();
      setMessage('');
      await onChange();
      return true;
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.');
      return false;
    }
  }

  return (
    <div className="mt-6 grid gap-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <SearchField value={query} onChange={setQuery} placeholder="Filtrar por nombre, código o vigencia" />
        <PrimaryButton onClick={() => { setMessage(''); setCreating(true); }}>
          <PlusIcon /> Nuevo
        </PrimaryButton>
      </div>
      <DataView
        columns={['Nombre', 'Vigencia', 'Orden', 'Reportes activos']}
        emptyLabel={categories.length === 0 ? 'Todavía no hay categorías.' : 'Ninguna categoría coincide con el filtro.'}
        rows={filtered.map((category) => ({
          id: category.id,
          cells: [
            <NameCell key="name" category={category} />,
            <span key="ttl">{formatVigencia(category.ttlMinutes) || 'Sin vigencia'}</span>,
            <span key="order">{category.sortOrder}</span>,
            <span key="count" className="tabular-nums">{category.activeReportCount ?? 0}</span>,
          ],
          actions: (
            <>
              <IconButton label={`Ver ${category.name}`} onClick={() => setViewing(category)}>
                <EyeIcon />
              </IconButton>
              <IconButton label={`Editar ${category.name}`} onClick={() => { setMessage(''); setEditing(category); }}>
                <PencilIcon />
              </IconButton>
            </>
          ),
        }))}
      />
      {creating ? (
        <Modal title="Nueva categoría" onClose={() => setCreating(false)}>
          <CreateForm
            message={message}
            onSubmit={async (body) => {
              if (await run(() => apiSend('admin/categories', 'POST', body))) {
                setCreating(false);
              }
            }}
          />
        </Modal>
      ) : null}
      {viewing ? (
        <Modal title={viewing.name} onClose={() => setViewing(null)}>
          <CategoryDetail category={viewing} onToggle={() => run(() => apiSend(`admin/categories/${viewing.id}/active`, 'POST', { isActive: !viewing.isActive }))} />
        </Modal>
      ) : null}
      {editing ? (
        <Modal title={`Editar ${editing.name}`} onClose={() => setEditing(null)}>
          <EditForm
            category={editing}
            message={message}
            onSave={(task) => run(task).then((saved) => { if (saved) setEditing(null); })}
            onAux={(task) => run(task)}
          />
        </Modal>
      ) : null}
    </div>
  );
}

function NameCell({ category }: { category: Category }) {
  return (
    <div className="flex min-w-0 items-center gap-3">
      <span className="h-8 w-8 shrink-0 rounded-lg border border-app-border" style={{ background: category.colorHex }} />
      <div className="min-w-0">
        <p className="truncate font-semibold">{category.name}</p>
        <p className="truncate text-xs text-app-muted">{category.code}</p>
        <div className="mt-1"><StatusPill on={category.isActive} onLabel="Activa" offLabel="Inactiva" /></div>
      </div>
    </div>
  );
}

function CategoryDetail({ category, onToggle }: { category: Category; onToggle: () => void }) {
  const facts = [
    ['Estado', category.isActive ? 'Activa' : 'Inactiva'],
    ['Código', category.code],
    ['Vigencia', formatVigencia(category.ttlMinutes) || 'Sin vigencia'],
    ['Orden', String(category.sortOrder)],
    ['Reportes activos', String(category.activeReportCount ?? 0)],
    ['Peso', String(category.importanceWeight)],
    ['Medio', mediaLabels[category.mediaRequirement] ?? category.mediaRequirement],
    ['Solo adultos', category.isAdultOnly ? 'Sí' : 'No'],
    ['Color', category.colorHex],
    ['Descripción', category.description],
    ['Traducciones', category.translations.map((item) => item.locale).join(', ') || 'Ninguna'],
  ];

  return (
    <div className="grid gap-4">
      <dl className="grid gap-3 sm:grid-cols-2">
        {facts.map(([label, value]) => (
          <div key={label}>
            <dt className="text-xs tracking-wide text-app-muted uppercase">{label}</dt>
            <dd className="mt-0.5">{value}</dd>
          </div>
        ))}
      </dl>
      <OverridePlaces overrides={category.overrides} />
      <div className="flex gap-4">
        {category.buttonIconUrl ? <img src={preview(category.buttonIconUrl)} alt="Ícono del botón" width={48} height={48} className="h-12 w-12 rounded-lg object-cover" /> : null}
        {category.mapIconUrl ? <img src={preview(category.mapIconUrl)} alt="Ícono del mapa" width={48} height={48} className="h-12 w-12 rounded-lg object-cover" /> : null}
      </div>
      <button type="button" className="justify-self-start rounded-lg border border-app-border px-3 py-2 text-sm" onClick={onToggle}>
        {category.isActive ? 'Desactivar' : 'Activar'}
      </button>
    </div>
  );
}

function CreateForm({ message, onSubmit }: { message: string; onSubmit: (body: Record<string, unknown>) => Promise<void> }) {
  const [localMessage, setLocalMessage] = useState('');
  return (
    <form className="grid gap-3 sm:grid-cols-2" onSubmit={(event) => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      const name = String(data.get('name') ?? '');
      const code = categoryCodeFromName(name);
      if (!code) {
        setLocalMessage('El nombre debe incluir al menos dos letras o números.');
        return;
      }

      const invalid = vigenciaError(Number(data.get('ttl')));
      if (invalid) {
        setLocalMessage(invalid);
        return;
      }

      setLocalMessage('');

      void onSubmit({
        actorId: emptyActor,
        code,
        name,
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
    }}>
      <label className="text-sm sm:col-span-2">Nombre<input name="name" required aria-label="Nombre" className={fieldClass} /></label>
      <label className="text-sm sm:col-span-2">Descripción<input name="description" required aria-label="Descripción" className={fieldClass} /></label>
      <label className="text-sm">Color<input name="color" required defaultValue="#FF6400" aria-label="Color" className={fieldClass} /></label>
      <label className="text-sm">Orden<input name="order" type="number" defaultValue={0} aria-label="Orden" className={fieldClass} /></label>
      <label className="text-sm">Vigencia (minutos)<input name="ttl" type="number" required min={minTtlMinutes} max={maxTtlMinutes} defaultValue={60} aria-label="Vigencia en minutos" className={fieldClass} /></label>
      <label className="text-sm">Peso<input name="weight" type="number" min={0} max={10} step="0.1" defaultValue={1} aria-label="Peso" className={fieldClass} /></label>
      <label className="text-sm sm:col-span-2">Medio requerido
        <select name="media" aria-label="Medio requerido" className={fieldClass}>
          <option value="None">Sin medio</option>
          <option value="PhotoRequired">Foto</option>
          <option value="VideoRequired">Video</option>
          <option value="PhotoOrVideoRequired">Foto o video</option>
        </select>
      </label>
      <label className="flex items-center gap-2 text-sm sm:col-span-2"><input name="adult" type="checkbox" /> Solo adultos</label>
      <div className="sm:col-span-2 flex justify-end">
        <PrimaryButton type="submit">Crear</PrimaryButton>
      </div>
      {localMessage || message ? <p role="alert" className="text-sm text-app-danger sm:col-span-2">{localMessage || message}</p> : null}
    </form>
  );
}

function EditForm({ category, message, onSave, onAux }: { category: Category; message: string; onSave: (task: () => Promise<unknown>) => Promise<void>; onAux: (task: () => Promise<unknown>) => Promise<unknown> }) {
  const [localMessage, setLocalMessage] = useState(message);
  const vigencia = formatVigencia(category.ttlMinutes);
  const idsKey = `${category.id}:${category.ttlMinutes}:${category.sortOrder}:${category.importanceWeight}:${category.name}`;

  return (
    <div className="grid gap-6">
      <form
        key={idsKey}
        aria-label={`Editar ${category.name}`}
        className="grid gap-3 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault();
          const data = new FormData(event.currentTarget);
          const ttl = Number(data.get('ttl'));
          const invalid = vigenciaError(ttl);
          if (invalid) {
            setLocalMessage(invalid);
            return;
          }

          const weight = Number(data.get('weight'));
          if (!Number.isFinite(weight) || weight < 0 || weight > 10) {
            setLocalMessage('El peso debe estar entre 0 y 10.');
            return;
          }

          setLocalMessage('');
          void onSave(async () => {
            await apiSend(`admin/categories/${category.id}`, 'PUT', {
              actorId: emptyActor,
              categoryId: category.id,
              name: String(data.get('name') || category.name),
              description: String(data.get('description') || category.description),
              buttonIconUrl: category.buttonIconUrl,
              mapIconUrl: category.mapIconUrl,
              colorHex: String(data.get('color') || category.colorHex),
              sortOrder: Number(data.get('order')),
              ttlMinutes: ttl,
              importanceWeight: weight,
              mediaRequirement: String(data.get('media') || category.mediaRequirement),
              isAdultOnly: data.get('adult') === 'on',
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
        }}
      >
        <label className="text-sm">Nombre<input name="name" required defaultValue={category.name} aria-label={`Nombre de ${category.name}`} className={fieldClass} /></label>
        <label className="text-sm">Descripción<input name="description" required defaultValue={category.description} aria-label={`Descripción de ${category.name}`} className={fieldClass} /></label>
        <label className="text-sm">
          Vigencia (minutos)
          <input name="ttl" type="number" required min={minTtlMinutes} max={maxTtlMinutes} step={1} defaultValue={category.ttlMinutes} aria-label={`Vigencia de ${category.name}`} className={fieldClass} />
          <span className="mt-1 block text-xs text-app-muted">Entre 5 minutos y 30 días. Guardada: {vigencia || 'sin vigencia'}.</span>
        </label>
        <label className="text-sm">Orden<input name="order" type="number" required defaultValue={category.sortOrder} aria-label={`Orden de ${category.name}`} className={fieldClass} /></label>
        <label className="text-sm">Peso de importancia<input name="weight" type="number" required min={0} max={10} step="0.1" defaultValue={category.importanceWeight} aria-label={`Peso de ${category.name}`} className={fieldClass} /></label>
        <label className="text-sm">Color<input name="color" required defaultValue={category.colorHex} aria-label={`Color de ${category.name}`} className={fieldClass} /></label>
        <label className="text-sm sm:col-span-2">Medio requerido
          <select name="media" defaultValue={category.mediaRequirement} aria-label={`Medio de ${category.name}`} className={fieldClass}>
            <option value="None">Sin medio</option>
            <option value="PhotoRequired">Foto</option>
            <option value="VideoRequired">Video</option>
            <option value="PhotoOrVideoRequired">Foto o video</option>
          </select>
        </label>
        <label className="flex items-center gap-2 text-sm sm:col-span-2"><input name="adult" type="checkbox" defaultChecked={category.isAdultOnly} aria-label={`Solo adultos de ${category.name}`} /> Solo adultos</label>
        <label className="text-sm">Ícono del botón<input name="button" type="file" accept="image/png,image/jpeg,image/webp" className="mt-1 block w-full text-sm" /></label>
        <label className="text-sm">Ícono del mapa<input name="map" type="file" accept="image/png,image/jpeg,image/webp" className="mt-1 block w-full text-sm" /></label>
        <div className="sm:col-span-2 flex justify-end"><PrimaryButton type="submit">Guardar</PrimaryButton></div>
        {localMessage || message ? <p role="alert" className="text-sm text-app-danger sm:col-span-2">{localMessage || message}</p> : null}
      </form>
      <TranslationForm category={category} onSubmit={onAux} />
      <OverrideForm category={category} onSubmit={onAux} />
    </div>
  );
}

function TranslationForm({ category, onSubmit }: { category: Category; onSubmit: (task: () => Promise<unknown>) => Promise<unknown> }) {
  return (
    <form className="grid gap-2 border-t border-app-border pt-4 sm:grid-cols-4" onSubmit={(event) => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      void onSubmit(() => apiSend(`admin/categories/${category.id}/translations`, 'PUT', { actorId: emptyActor, categoryId: category.id, locale: data.get('locale'), name: data.get('name'), description: data.get('description') }));
    }}>
      <p className="text-sm font-semibold sm:col-span-4">Traducción</p>
      <input name="locale" required placeholder="es-CO" aria-label="Idioma" className={fieldClass} />
      <input name="name" required placeholder="Nombre traducido" aria-label="Nombre traducido" className={fieldClass} />
      <input name="description" required placeholder="Descripción" aria-label="Descripción traducida" className={fieldClass} />
      <button type="submit" className="rounded-lg border border-app-border px-3 py-2 text-sm">Agregar</button>
    </form>
  );
}

function OverrideForm({ category, onSubmit }: { category: Category; onSubmit: (task: () => Promise<unknown>) => Promise<unknown> }) {
  return (
    <form className="grid gap-3 border-t border-app-border pt-4" onSubmit={(event) => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      const countryId = String(data.get('country') || '');
      const cityId = String(data.get('city') || '');
      if (!countryId && !cityId) {
        return;
      }

      void onSubmit(() => apiSend(`admin/categories/${category.id}/overrides`, 'PUT', {
        actorId: emptyActor,
        categoryId: category.id,
        countryId: countryId || null,
        cityId: cityId || null,
        isEnabled: data.get('enabled') === 'on',
        ttlMinutes: data.get('ttl') ? Number(data.get('ttl')) : null,
      }));
    }}>
      <p className="text-sm font-semibold">Excepción por lugar</p>
      <OverridePlaces overrides={category.overrides} />
      <PlaceFields />
      <label className="text-sm">Vigencia de la excepción (minutos)
        <input name="ttl" type="number" min={minTtlMinutes} max={maxTtlMinutes} placeholder="Usa la de la categoría" aria-label="Vigencia de la excepción" className={fieldClass} />
      </label>
      <label className="flex items-center gap-2 text-sm"><input name="enabled" type="checkbox" defaultChecked /> Activa</label>
      <button type="submit" className="justify-self-end rounded-lg border border-app-border px-3 py-2 text-sm">Guardar excepción</button>
    </form>
  );
}

function OverridePlaces({ overrides }: { overrides: Category['overrides'] }) {
  const [loaded, setLoaded] = useState<string | null>(null);
  const text = overrides.length === 0 ? 'Ninguna' : loaded ?? 'Cargando lugares…';

  useEffect(() => {
    if (overrides.length === 0) {
      return;
    }

    let active = true;
    void describeOverrides(overrides).then((value) => {
      if (active) setLoaded(value);
    });
    return () => {
      active = false;
    };
  }, [overrides]);

  return (
    <div>
      <p className="text-xs tracking-wide text-app-muted uppercase">Excepciones</p>
      <p className="mt-0.5 text-sm">{text}</p>
    </div>
  );
}

async function describeOverrides(overrides: Category['overrides']) {
  try {
    const countries = (await apiGet<{ countries?: { id: string; name: string }[] }>('geography/countries'))?.countries ?? [];
    const countryNames = new Map(countries.map((country) => [country.id, country.name]));
    const lines: string[] = [];
    for (const item of overrides) {
      const country = item.countryId ? countryNames.get(item.countryId) : undefined;
      const city = item.countryId && item.cityId ? await cityName(item.countryId, item.cityId) : undefined;
      const place = [country, city].filter(Boolean).join(', ') || 'Lugar guardado';
      const life = item.ttlMinutes ? formatVigencia(item.ttlMinutes) : 'vigencia de la categoría';
      lines.push(`${place} · ${life} · ${item.isEnabled ? 'activa' : 'inactiva'}`);
    }
    return lines.join('; ');
  } catch {
    return 'Excepciones guardadas';
  }
}

async function cityName(countryId: string, cityId: string) {
  const regions = (await apiGet<{ regions?: { id: string; name: string }[] }>(`geography/countries/${countryId}/regions`))?.regions ?? [];
  for (const region of regions) {
    const cities = (await apiGet<{ cities?: { id: string; name: string }[] }>(`geography/regions/${region.id}/cities`))?.cities ?? [];
    const city = cities.find((item) => item.id === cityId);
    if (city) return city.name;
  }
  return undefined;
}

function PlaceFields() {
  const [countries, setCountries] = useState<{ id: string; name: string }[]>([]);
  const [regions, setRegions] = useState<{ id: string; name: string }[]>([]);
  const [cities, setCities] = useState<{ id: string; name: string }[]>([]);
  const [countryId, setCountryId] = useState('');
  const [regionId, setRegionId] = useState('');

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const data = await apiGet<{ countries?: { id: string; name: string }[] }>('geography/countries');
        if (active) setCountries(data?.countries ?? []);
      } catch {
        if (active) setCountries([]);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!countryId) {
      return;
    }

    let active = true;
    void (async () => {
      try {
        const data = await apiGet<{ regions?: { id: string; name: string }[] }>(`geography/countries/${countryId}/regions`);
        if (active) setRegions(data?.regions ?? []);
      } catch {
        if (active) setRegions([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [countryId]);

  useEffect(() => {
    if (!regionId) {
      return;
    }

    let active = true;
    void (async () => {
      try {
        const data = await apiGet<{ cities?: { id: string; name: string }[] }>(`geography/regions/${regionId}/cities`);
        if (active) setCities(data?.cities ?? []);
      } catch {
        if (active) setCities([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [regionId]);

  return (
    <div className="grid gap-3 sm:grid-cols-3">
      <label className="text-sm">País
        <select name="country" aria-label="País" value={countryId} className={fieldClass} onChange={(event) => { setCountryId(event.target.value); setRegionId(''); setCities([]); }}>
          <option value="">Selecciona un país</option>
          {countries.map((country) => <option key={country.id} value={country.id}>{country.name}</option>)}
        </select>
      </label>
      <label className="text-sm">Departamento
        <select aria-label="Departamento" value={regionId} disabled={!countryId} className={fieldClass} onChange={(event) => setRegionId(event.target.value)}>
          <option value="">Selecciona un departamento</option>
          {regions.map((region) => <option key={region.id} value={region.id}>{region.name}</option>)}
        </select>
      </label>
      <label className="text-sm">Ciudad
        <select name="city" aria-label="Ciudad" disabled={!regionId} className={fieldClass}>
          <option value="">Todo el país</option>
          {cities.map((city) => <option key={city.id} value={city.id}>{city.name}</option>)}
        </select>
      </label>
    </div>
  );
}
