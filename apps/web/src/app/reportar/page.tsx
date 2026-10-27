import type { Metadata } from 'next';
import Link from 'next/link';

export const metadata: Metadata = { title: 'Reportar' };

export default async function ReportPage({ searchParams }: { searchParams: Promise<{ category?: string }> }) {
  const params = await searchParams;
  return (
    <main className="mx-auto grid min-h-dvh max-w-lg place-items-center gap-4 px-6 text-center">
      <div>
        <h1 className="font-display text-3xl">Publicar un chisme</h1>
        <p className="mt-3 text-app-muted">El formulario para crear el reporte llega en el siguiente paso. La categoría ya quedó elegida.</p>
        {params.category ? <p className="mt-2 text-sm">Categoría preparada.</p> : null}
        <Link href="/mapa" className="mt-6 inline-flex min-h-11 items-center font-bold text-primary">Volver al mapa</Link>
      </div>
    </main>
  );
}
