'use client';

import { DataView, EyeIcon, IconButton, PageHeader, PrimaryButton, fieldClass } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
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

  const users = search.data?.users ?? [];
  return (
    <section>
      <PageHeader title="Usuarios" hint="Busca por usuario o por el correo exacto." />
      <form className="mt-6 flex flex-col gap-3 sm:flex-row sm:items-end" onSubmit={(event) => { event.preventDefault(); setSubmitted(query.trim()); }}>
        <label className="min-w-0 flex-1 text-sm">Buscar
          <input minLength={3} value={query} onChange={(event) => setQuery(event.target.value)} className={fieldClass} />
        </label>
        <PrimaryButton type="submit">Buscar</PrimaryButton>
      </form>
      <PanelState loading={search.isFetching && submitted.length >= 3} error={search.error}>
        {submitted ? (
          <div className="mt-4">
            <DataView
              columns={['Usuario', 'Correo', 'Estado']}
              emptyLabel="No hay cuentas con esa búsqueda."
              rows={users.map((user) => ({
                id: user.id,
                cells: [
                  <span key="name" className="font-semibold">{user.userName}{user.isMinor ? ' · menor' : ''}</span>,
                  <span key="email" className="break-all">{user.email}</span>,
                  user.status,
                ],
                actions: <IconButton label={`Ver ${user.userName}`} href={`/usuarios/${user.id}`}><EyeIcon /></IconButton>,
              }))}
            />
          </div>
        ) : null}
      </PanelState>
    </section>
  );
}
