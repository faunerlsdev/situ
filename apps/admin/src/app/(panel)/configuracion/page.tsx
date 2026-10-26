'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

type Settings = { groups: { name: string; items: { key: string; value: string; description?: string; readOnly: boolean }[] }[] };
type Countries = { countries: { id: string; name: string; minimumAge: number }[] };

export default function ConfiguracionPage() {
  const me = useMe();
  const client = useQueryClient();
  const [message, setMessage] = useState('');
  const query = useQuery({ queryKey: ['settings'], queryFn: () => apiGet<Settings>('admin/settings'), enabled: can(me.permissions, 'settings.manage') });
  const countries = useQuery({ queryKey: ['countries'], queryFn: () => apiGet<Countries>('geography/countries'), enabled: can(me.permissions, 'settings.manage') });
  if (!can(me.permissions, 'settings.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  async function save(key: string, value: string) {
    try {
      await apiSend(`admin/settings/${encodeURIComponent(key)}`, 'PUT', { value });
      setMessage('Guardado.');
      await client.invalidateQueries({ queryKey: ['settings'] });
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.');
    }
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Configuración</h1>
      <p className="mt-2 text-sm text-app-muted">Las claves de secretos, tokens y correo no aparecen aquí.</p>
      {message ? <p role="status" className="mt-2">{message}</p> : null}
      <PanelState loading={query.isLoading} error={query.error} empty={(query.data?.groups.length ?? 0) === 0}>
        <div className="mt-4 grid gap-6">
          {query.data?.groups.map((group) => (
            <section key={group.name}>
              <h2 className="font-semibold capitalize">{group.name}</h2>
              <ul className="mt-2 grid gap-3">
                {group.items.map((item) => (
                  <li key={item.key}>
                    <SettingField item={item} onSave={save} />
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      </PanelState>
      <section className="mt-8">
        <h2 className="font-semibold">Edad mínima por país</h2>
        <ul className="mt-2 grid gap-2">
          {countries.data?.countries.map((country) => (
            <li key={country.id}>
              <form className="flex flex-wrap items-center gap-2" onSubmit={(event) => {
                event.preventDefault();
                const age = Number(new FormData(event.currentTarget).get('age'));
                void apiSend(`admin/countries/${country.id}/minimum-age`, 'PUT', { minimumAge: age }).then(() => setMessage('Edad mínima actualizada.')).catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos guardar.'));
              }}>
                <span className="min-w-0 sm:w-40">{country.name}</span>
                <input name="age" type="number" min={13} max={18} defaultValue={country.minimumAge} aria-label={`Edad mínima en ${country.name}`} className="w-20 rounded border border-app-border bg-app-input px-2 py-1" />
                <button type="submit" className="rounded border border-app-border px-3 py-1">Guardar</button>
              </form>
            </li>
          ))}
        </ul>
      </section>
    </section>
  );
}

function SettingField({ item, onSave }: { item: { key: string; value: string; description?: string; readOnly: boolean }; onSave: (key: string, value: string) => Promise<void> }) {
  const [value, setValue] = useState(item.value);
  const wide = item.key.includes('copy') || item.key.includes('url') || value.length > 80;
  return (
    <form className="grid gap-1" onSubmit={(event) => { event.preventDefault(); void onSave(item.key, value); }}>
      <label htmlFor={item.key} className="text-sm text-app-muted">{item.key}</label>
      {item.description ? <p className="text-xs text-app-muted">{item.description}</p> : null}
      {wide ? (
        <textarea id={item.key} value={value} disabled={item.readOnly} onChange={(event) => setValue(event.target.value)} className="min-h-24 rounded border border-app-border bg-app-input p-2 font-mono text-sm" />
      ) : (
        <input id={item.key} value={value} disabled={item.readOnly} onChange={(event) => setValue(event.target.value)} className="rounded border border-app-border bg-app-input px-3 py-2" />
      )}
      {item.readOnly ? null : <button type="submit" className="w-fit rounded bg-app-primary px-3 py-1 font-semibold text-app-on">Guardar</button>}
    </form>
  );
}
