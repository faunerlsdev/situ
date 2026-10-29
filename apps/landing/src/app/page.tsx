import { sites } from '@situ/shared';

const steps = [
  {
    title: 'Eliges tu ciudad',
    text: 'El registro te pide país, región y ciudad. Esa ciudad es la de tus avisos.',
  },
  {
    title: 'Ves lo que pasa cerca',
    text: 'El mapa muestra reportes de tu ciudad y de un radio de 1 km. También te llegan los de tus amigos.',
  },
  {
    title: 'Confirmas y comentas',
    text: 'Alguien dice si sigue ahí o si ya no está. Tú puedes reaccionar y dejar un comentario, con nombre o en anónimo.',
  },
  {
    title: 'El vivo sale del celular',
    text: 'La transmisión se inicia solo en la app. En la web alcanzas a verla y a comentarla.',
  },
] as const;

const protections = [
  'Tratamos los datos personales bajo la Ley 1581 de 2012.',
  'Quien aún no es mayor de edad tiene perfil privado y no aparece en el mapa público.',
  'Un menor solo puede transmitir a sus amigos, siempre en anónimo. Antes de los 16 hace falta la autorización del acudiente.',
  'El acudiente tiene una ventana de 7 días para responder.',
  'No hacemos publicidad personalizada a menores.',
] as const;

const plans = [
  { name: 'Free', text: 'Mapa, reportes y vivos con publicidad.' },
  { name: 'Basic', text: 'Sin publicidad, con más espacio para reportar y transmitir.' },
  { name: 'Pro', text: 'Reportes y anónimos sin tope, y pines patrocinados.' },
  { name: 'Advanced', text: 'Sin topes de uso y con pines patrocinados.' },
] as const;

export default function LandingPage() {
  return (
    <>
      <section id="inicio" className="mx-auto grid w-full max-w-6xl gap-10 px-5 py-16 lg:grid-cols-[1.2fr_0.8fr] lg:items-center">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-primary-light">Colombia, en tiempo real</p>
          <h1 className="mt-4 font-display text-5xl leading-tight sm:text-6xl">Qué está pasando, a la vuelta de tu casa.</h1>
          <p className="mt-6 max-w-xl text-lg leading-8 text-gray600">
            SITU es la red para reportar accidentes, retenes, hurtos o basura, avisar a tu ciudad y a quien está a un
            kilómetro, y comentarlo con amigos. El chisme, con mapa.
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <a
              href="#descarga"
              className="inline-flex min-h-11 items-center rounded-full bg-primary px-5 font-semibold text-white hover:bg-primary-pressed"
            >
              Quiero usarla
            </a>
            <a
              href={`${sites.web}/mapa`}
              className="inline-flex min-h-11 items-center rounded-full border border-ink px-5 font-semibold hover:bg-ink hover:text-white"
            >
              Ver la web
            </a>
          </div>
        </div>
        <div className="rounded-[1.75rem] bg-ink p-6 text-white">
          <div className="relative h-64" aria-hidden="true">
            <div className="absolute inset-x-6 top-16 h-2 rounded-full bg-white/15" />
            <div className="absolute inset-y-6 left-1/2 w-2 rounded-full bg-white/15" />
            <span className="absolute left-10 top-20 h-4 w-4 rounded-full bg-pin" />
            <span className="absolute right-16 top-36 h-4 w-4 rounded-full bg-primary-light" />
            <span className="absolute bottom-10 left-24 h-4 w-4 rounded-full bg-live" />
          </div>
          <p className="text-sm text-white/70">Pines de ejemplo. El mapa real se llena con lo que reporta la gente.</p>
        </div>
      </section>

      <section id="como-funciona" className="border-y border-gray200 bg-white">
        <div className="mx-auto w-full max-w-6xl px-5 py-16">
          <h2 className="font-display text-4xl">Cómo funciona</h2>
          <ol className="mt-8 grid gap-4 sm:grid-cols-2">
            {steps.map((step, index) => (
              <li key={step.title} className="rounded-3xl border border-gray200 bg-surface p-5">
                <p className="text-sm font-semibold text-primary-light">Paso {index + 1}</p>
                <h3 className="mt-2 font-display text-2xl">{step.title}</h3>
                <p className="mt-2 leading-7 text-gray600">{step.text}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section id="seguridad" className="mx-auto w-full max-w-6xl px-5 py-16">
        <div className="grid gap-8 lg:grid-cols-[0.8fr_1.2fr]">
          <div>
            <h2 className="font-display text-4xl">Seguridad y menores</h2>
            <p className="mt-4 leading-7 text-gray600">
              Si hay que elegir entre una función y la privacidad de un menor, gana la privacidad.
            </p>
          </div>
          <ul className="space-y-3">
            {protections.map((item) => (
              <li key={item} className="rounded-2xl border border-gray200 bg-white px-4 py-4 leading-7">
                {item}
              </li>
            ))}
          </ul>
        </div>
        <p className="mt-6">
          <a
            href={`${sites.support}/contacto`}
            className="font-semibold text-ink underline-offset-2 hover:underline"
          >
            Consultas de privacidad
          </a>
          {' · '}
          <a
            href={`${sites.support}/retiro-contenido`}
            className="font-semibold text-ink underline-offset-2 hover:underline"
          >
            Retiro de contenido
          </a>
        </p>
      </section>

      <section id="planes" className="border-y border-gray200 bg-white">
        <div className="mx-auto w-full max-w-6xl px-5 py-16">
          <h2 className="font-display text-4xl">Planes</h2>
          <p className="mt-3 max-w-2xl text-gray600">
            Free, Basic, Pro y Advanced. Los topes viven en el servidor y pueden cambiar sin una nueva versión de la app.
          </p>
          <ul className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {plans.map((plan) => (
              <li key={plan.name} className="rounded-3xl border border-gray200 bg-surface p-5">
                <h3 className="font-display text-2xl">{plan.name}</h3>
                <p className="mt-3 leading-7 text-gray600">{plan.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section id="descarga" className="mx-auto w-full max-w-6xl px-5 py-16">
        <h2 className="font-display text-4xl">Descarga</h2>
        <p className="mt-4 max-w-2xl text-lg leading-8 text-gray600">
          La app para iPhone y Android está en camino. Mientras tanto puedes entrar a la versión web: ahí se ve el mapa
          y se comenta. El vivo, cuando esté la app, se inicia desde el celular.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <a
            href={sites.web}
            className="inline-flex min-h-11 items-center rounded-full bg-ink px-5 font-semibold text-white"
          >
            Abrir la versión web
          </a>
          <p className="inline-flex min-h-11 items-center rounded-full border border-gray200 px-5 text-sm text-gray600">
            App Store y Google Play: próximamente
          </p>
        </div>
      </section>
    </>
  );
}
