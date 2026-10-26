'use client';

import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { count, type Dashboard } from '@/lib/api-contract';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';

export default function ResumenPage() {
  const me = useMe();
  const query = useQuery({ queryKey: ['dashboard'], queryFn: () => apiGet<Dashboard>('admin/dashboard'), enabled: can(me.permissions, 'metrics.view') });
  if (!can(me.permissions, 'metrics.view')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  const data = query.data;
  return (
    <section>
      <h1 className="font-display text-3xl">Resumen</h1>
      <PanelState loading={query.isLoading} error={query.error}>
        {data ? (
          <>
            <div className="mt-6 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Card label="Usuarios" value={count(data.usersTotal)} />
              <Card label="Activos en 7 días" value={count(data.usersActiveIn7Days)} />
              <Card label="Reportes activos" value={count(data.reportsActive)} />
              <Card label="Eliminaciones pendientes" value={count(data.deletionRequestsPending)} />
            </div>
            <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Link href="/denuncias" className="rounded-xl border border-app-border bg-app-surface p-4">
                <p className="text-app-muted">Denuncias abiertas</p>
                <p className="font-display text-3xl">{count(data.complaintsOpen)}</p>
                <p className="text-sm text-app-muted">{count(data.complaintsPriority)} prioritarias</p>
              </Link>
              <Link href="/retiros" className="rounded-xl border border-app-border bg-app-surface p-4">
                <p className="text-app-muted">Retiros por vencer o vencidos</p>
                <p className="font-display text-3xl">{count(data.takedownsDueSoon) + count(data.takedownsOverdue)}</p>
                <p className="text-sm text-app-muted">{count(data.takedownsOverdue)} vencidos</p>
              </Link>
            </div>
            <h2 className="mt-6 font-semibold">Reportes por categoría, últimos 7 días</h2>
            <div className="mt-2 overflow-x-auto">
            <table className="w-full min-w-[20rem] text-left text-sm">
              <thead><tr className="text-app-muted"><th className="py-2">Categoría</th><th>Cantidad</th></tr></thead>
              <tbody>
                {data.reportsByCategoryIn7Days.map((row) => (
                  <tr key={row.code} className="border-t border-app-border"><td className="py-2">{row.code}</td><td>{row.count}</td></tr>
                ))}
              </tbody>
            </table>
            </div>
          </>
        ) : null}
      </PanelState>
    </section>
  );
}

function Card({ label, value }: { label: string; value: number }) {
  return (
    <article className="rounded-xl border border-app-border bg-app-surface p-4">
      <p className="text-sm text-app-muted">{label}</p>
      <p className="font-display text-3xl">{value}</p>
    </article>
  );
}
