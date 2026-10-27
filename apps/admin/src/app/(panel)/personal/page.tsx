'use client';

import { DataView, EyeIcon, IconButton, Modal, PageHeader, PencilIcon, PlusIcon, PrimaryButton, SearchField, StatusPill, fieldClass } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

type Member = { assignmentId: string; userId: string; userName: string; roleName: string; isActive: boolean };
type Staff = { staff: Member[] };
const roles = ['SuperAdmin', 'Moderator', 'Support', 'ContentManager', 'Auditor', 'Finance'];

export default function PersonalPage() {
  const me = useMe();
  const client = useQueryClient();
  const [query, setQuery] = useState('');
  const [message, setMessage] = useState('');
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<Member | null>(null);
  const [editing, setEditing] = useState<Member | null>(null);
  const [resetId, setResetId] = useState<string | null>(null);
  const list = useQuery({ queryKey: ['staff'], queryFn: () => apiGet<Staff>('admin/staff'), enabled: can(me.permissions, 'staff.manage') });
  const people = list.data?.staff ?? [];
  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return people;
    return people.filter((member) => `${member.userName} ${member.roleName}`.toLowerCase().includes(needle));
  }, [people, query]);

  if (!can(me.permissions, 'staff.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  async function refresh() {
    await client.invalidateQueries({ queryKey: ['staff'] });
  }

  return (
    <section>
      <PageHeader title="Personal" hint="Cuentas con acceso al panel." action={
        <PrimaryButton onClick={() => { setMessage(''); setCreating(true); }}><PlusIcon /> Nuevo</PrimaryButton>
      } />
      {message ? <p role="status" className="mt-3 text-sm">{message}</p> : null}
      <PanelState loading={list.isLoading} error={list.error}>
        <div className="mt-6 grid gap-4">
          <SearchField value={query} onChange={setQuery} placeholder="Filtrar por usuario o rol" />
          <DataView
            columns={['Usuario', 'Rol', 'Estado']}
            emptyLabel={people.length === 0 ? 'Todavía no hay personal.' : 'Nadie coincide con el filtro.'}
            rows={filtered.map((member) => ({
              id: member.assignmentId,
              cells: [
                <span key="name" className="font-semibold">{member.userName}</span>,
                member.roleName,
                <StatusPill key="state" on={member.isActive} onLabel="Activo" offLabel="Inactivo" />,
              ],
              actions: (
                <>
                  <IconButton label={`Ver ${member.userName}`} onClick={() => setViewing(member)}><EyeIcon /></IconButton>
                  <IconButton label={`Editar ${member.userName}`} onClick={() => setEditing(member)}><PencilIcon /></IconButton>
                </>
              ),
            }))}
          />
        </div>
      </PanelState>
      {creating ? (
        <Modal title="Asignar rol" onClose={() => setCreating(false)}>
          <form className="grid gap-3" onSubmit={(event) => {
            event.preventDefault();
            const data = new FormData(event.currentTarget);
            void apiSend('admin/staff', 'POST', { email: data.get('email'), role: data.get('role') })
              .then(async () => { setMessage('Rol asignado.'); setCreating(false); await refresh(); })
              .catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos asignar el rol.'));
          }}>
            <label className="text-sm">Correo de una cuenta existente<input name="email" type="email" required aria-label="Correo" className={fieldClass} /></label>
            <label className="text-sm">Rol
              <select name="role" aria-label="Rol" className={fieldClass}>{roles.map((role) => <option key={role}>{role}</option>)}</select>
            </label>
            <div className="flex justify-end"><PrimaryButton type="submit">Asignar</PrimaryButton></div>
          </form>
        </Modal>
      ) : null}
      {viewing ? (
        <Modal title={viewing.userName} onClose={() => setViewing(null)}>
          <dl className="grid gap-3 sm:grid-cols-2">
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Usuario</dt><dd>{viewing.userName}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Rol</dt><dd>{viewing.roleName}</dd></div>
            <div><dt className="text-xs tracking-wide text-app-muted uppercase">Estado</dt><dd>{viewing.isActive ? 'Activo' : 'Inactivo'}</dd></div>
          </dl>
        </Modal>
      ) : null}
      {editing ? (
        <Modal title={`Editar ${editing.userName}`} onClose={() => setEditing(null)}>
          <p className="text-sm text-app-muted">Rol actual: {editing.roleName}. Revocar quita el acceso de esta asignación.</p>
          <div className="mt-4 flex flex-wrap gap-2">
            <button type="button" className="rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => {
              void apiSend(`admin/staff/${editing.assignmentId}`, 'DELETE')
                .then(async () => { setEditing(null); setMessage('Rol revocado.'); await refresh(); })
                .catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos revocar el rol.'));
            }}>Revocar</button>
            {editing.userId !== me.userId ? (
              <button type="button" className="rounded-lg border border-app-border px-3 py-2 text-sm" onClick={() => setResetId(editing.userId)}>Restablecer 2FA</button>
            ) : null}
          </div>
        </Modal>
      ) : null}
      {resetId ? (
        <ReasonDialog title="Restablecer el segundo factor" confirmLabel="Restablecer" danger onClose={() => setResetId(null)} onConfirm={async (reason) => {
          await apiSend('admin/2fa/reset', 'POST', { userId: resetId, reason });
          setResetId(null);
          setEditing(null);
          setMessage('Esa cuenta tendrá que inscribir de nuevo el autenticador.');
        }} />
      ) : null}
    </section>
  );
}
