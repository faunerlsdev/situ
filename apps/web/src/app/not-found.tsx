import Link from 'next/link';

export default function NotFound() {
  return (
    <section className="mx-auto w-full max-w-3xl px-5 py-20">
      <p className="text-sm font-semibold uppercase tracking-widest text-primary-light">404</p>
      <h1 className="mt-3 font-display text-5xl">Esa página no está en el mapa.</h1>
      <p className="mt-4 max-w-xl text-lg text-gray600">
        El enlace no existe o se movió. Vuelve al inicio o abre el mapa.
      </p>
      <div className="mt-8 flex flex-wrap gap-3">
        <Link href="/" className="inline-flex min-h-11 items-center rounded-full bg-ink px-5 font-semibold text-white">
          Ir al inicio
        </Link>
        <Link
          href="/mapa"
          className="inline-flex min-h-11 items-center rounded-full border border-ink px-5 font-semibold"
        >
          Abrir el mapa
        </Link>
      </div>
    </section>
  );
}
