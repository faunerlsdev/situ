'use client';

import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { ReportPanel } from '@/features/reports/report-panel';
import { readBrowserSession } from '@/lib/http';

export function ReportPageClient({ id }: { id: string }) {
  const session = useQuery({
    queryKey: ['session'],
    queryFn: () => readBrowserSession(),
  });

  return (
    <main className="mx-auto grid w-full max-w-lg gap-4 px-4 py-6">
      <Link href="/mapa" className="min-h-11 font-bold text-primary">Volver al mapa</Link>
      <ReportPanel id={id} signedIn={session.data?.signedIn === true} here={null} />
    </main>
  );
}
