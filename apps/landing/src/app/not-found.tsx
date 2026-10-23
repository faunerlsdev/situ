import Link from 'next/link';

export default function NotFound() {
  return (
    <section className="mx-auto w-full max-w-3xl px-5 py-20">
      <h1 className="font-display text-5xl">No encontramos esa página.</h1>
      <p className="mt-4 text-gray600">Vuelve al inicio para ver cómo funciona SITU.</p>
      <Link href="/" className="mt-8 inline-flex min-h-11 items-center rounded-full bg-ink px-5 font-semibold text-white">
        Ir al inicio
      </Link>
    </section>
  );
}
