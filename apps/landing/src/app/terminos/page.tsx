import type { Metadata } from 'next';

export const metadata: Metadata = { title: 'Términos' };

export default function TermsPage() {
  return (
    <article className="mx-auto w-full max-w-3xl px-5 py-16">
      <h1 className="font-display text-5xl">Términos</h1>
      <div className="mt-6 space-y-4 leading-8 text-gray600">
        <p>
          SITU es una red para contar lo que ocurre en un lugar. Tú respondes por lo que publicas. No subas datos de
          otras personas ni contenido que ponga en riesgo a un menor.
        </p>
        <p>
          La web sirve para ver y comentar. Iniciar una transmisión es una función de la app móvil. Los planes y sus
          límites los define el servidor.
        </p>
        <p>Este resumen no reemplaza el contrato que aceptas al crear la cuenta.</p>
      </div>
    </article>
  );
}
