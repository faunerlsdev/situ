import type { Metadata } from 'next';
import { RequestForm } from '@/components/request-form';

export const metadata: Metadata = { title: 'Habeas data y retiro' };

export default function RequestsPage() {
  return (
    <section className="mx-auto w-full max-w-xl px-5 py-12">
      <h1 className="font-display text-5xl">Habeas data y retiro de contenido</h1>
      <p className="mt-3 leading-7 text-gray600">
        Pide conocer, corregir o eliminar tus datos, o solicita que retiremos una publicación. Respondemos al correo
        que dejes en el formulario. Ley 1581 de 2012.
      </p>
      <div className="mt-8">
        <RequestForm />
      </div>
    </section>
  );
}
