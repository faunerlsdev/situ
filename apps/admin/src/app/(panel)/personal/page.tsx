'use client';

import { useMe } from '@/components/frame';
import { ReasonDialog } from '@/components/reason-dialog';
import { PanelState } from '@/components/states';
import { apiGet, apiSend } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

type Staff = { staff: { assignmentId: string; userId: string; userName: string; roleName: string; isActive: boolean }[] };
const roles = ['SuperAdmin', 'Moderator', 'Support', 'ContentManager', 'Auditor', 'Finance'];

export default function PersonalPage() {
  const me = useMe();
  const client = useQueryClient();
  const [message, setMessage] = useState('');
  const [resetId, setResetId] = useState<string | null>(null);
  const query = useQuery({ queryKey: ['staff'], queryFn: () => apiGet<Staff>('admin/staff'), enabled: can(me.permissions, 'staff.manage') });
  if (!can(me.permissions, 'staff.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <h1 className="font-display text-3xl">Personal</h1>
      {message ? <p role="status" className="mt-2">{message}</p> : null}
      <PanelState loading={query.isLoading} error={query.error} empty={(query.data?.staff.length ?? 0) === 0}>
        <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[32rem] text-left text-sm">
          <thead><tr className="text-app-muted"><th className="py-2">Usuario</th><th>Rol</th><th /></tr></thead>
          <tbody>
            {query.data?.staff.map((member) => (
              <tr key={member.assignmentId} className="border-t border-app-border">
                <td className="py-2">{member.userName}</td>
                <td>{member.roleName}</td>
                <td className="flex gap-3">
                  <button type="button" onClick={() => void apiSend(`admin/staff/${member.assignmentId}`, 'DELETE').then(() => client.invalidateQueries({ queryKey: ['staff'] })).catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos revocar el rol.'))}>Revocar</button>
                  {member.userId !== me.userId ? <button type="button" onClick={() => setResetId(member.userId)}>Restablecer 2FA</button> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
      </PanelState>
      <form className="mt-8 flex max-w-xl flex-col gap-2 sm:flex-row" onSubmit={(event) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        void apiSend('admin/staff', 'POST', { email: data.get('email'), role: data.get('role') })
          .then(() => { setMessage('Rol asignado.'); event.currentTarget.reset(); return client.invalidateQueries({ queryKey: ['staff'] }); })
          .catch((cause: unknown) => setMessage(cause instanceof Error ? cause.message : 'No pudimos asignar el rol.'));
      }}>
        <input name="email" type="email" required placeholder="Correo de una cuenta existente" aria-label="Correo" className="w-full flex-1 rounded border border-app-border bg-app-input px-3 py-2" />
        <select name="role" aria-label="Rol" className="rounded border border-app-border bg-app-input px-3 py-2">{roles.map((role) => <option key={role}>{role}</option>)}</select>
        <button type="submit" className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Asignar</button>
      </form>
      {resetId ? (
        <ReasonDialog title="Restablecer el segundo factor" confirmLabel="Restablecer" danger onClose={() => setResetId(null)} onConfirm={async (reason) => {
          await apiSend('admin/2fa/reset', 'POST', { userId: resetId, reason });
          setResetId(null);
          setMessage('Esa cuenta tendrá que inscribir de nuevo el autenticador.');
        }} />
      ) : null}
    </section>
  );
}
