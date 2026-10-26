'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useState } from 'react';

type Search = { users: { id: string; userName: string; email: string; status: string; isMinor: boolean }[] };

export default function UsuariosPage() {
  const me = useMe();
  const [query, setQuery] = useState('');
  const [submitted, setSubmitted] = useState('');
  const search = useQuery({
    queryKey: ['users', submitted],
    queryFn: () => apiGet<Search>(`admin/users?query=${encodeURIComponent(submitted)}`),
    enabled: submitted.length >= 3 && can(me.permissions, 'users.view'),
  });
  if (!can(me.permissions, 'users.view')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Usuarios</h1>
      <form className="mt-4 flex flex-col gap-2 sm:flex-row sm:items-end" onSubmit={(event) => { event.preventDefault(); setSubmitted(query.trim()); }}>
        <label className="flex-1">Buscar por usuario o correo exacto
          <input minLength={3} value={query} onChange={(event) => setQuery(event.target.value)} className="mt-1 w-full rounded border border-app-border bg-app-input px-3 py-2" />
        </label>
        <button type="submit" className="self-end rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Buscar</button>
      </form>
      <PanelState loading={search.isFetching} error={search.error} empty={Boolean(submitted) && (search.data?.users.length ?? 0) === 0}>
        <ul className="mt-4 grid gap-2">
          {search.data?.users.map((user) => (
            <li key={user.id} className="break-all"><Link href={`/usuarios/${user.id}`} className="font-semibold text-app-primary">{user.userName}</Link> · {user.email} · {user.status}{user.isMinor ? ' · menor' : ''}</li>
          ))}
        </ul>
      </PanelState>
    </section>
  );
}
