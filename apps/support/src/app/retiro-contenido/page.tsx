import type { Metadata } from 'next';
import Link from 'next/link';
import { TakedownForm } from '@/components/takedown-form';

export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'Retiro de contenido',
  description: 'Pide la revisión y el retiro de una publicación. Respondemos en 15 días.',
  robots: { index: true, follow: true },
};

export default function ContentRemovalPage() {
  return (
    <section className="mx-auto w-full max-w-xl px-5 py-12">
      <h1 className="font-display text-5xl">Retiro de contenido</h1>
      <p className="mt-3 leading-7 text-gray600">
        Si una publicación vulnera tus derechos, pide que la revisemos. Para consultas, corrección o supresión de tus datos usa el{' '}
        <Link href="/contacto" className="font-semibold text-ink underline-offset-2 hover:underline">formulario de privacidad</Link>.
      </p>
      <div className="mt-8">
        <TakedownForm />
      </div>
    </section>
  );
}
