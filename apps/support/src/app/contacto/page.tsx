import type { Metadata } from 'next';
import { ContactForm } from '@/components/contact-form';

export const metadata: Metadata = {
  title: 'Contacto',
  description: 'Escribe a SITU. Respondemos en 2 días hábiles.',
  robots: { index: true, follow: true },
};

const domainRoot = (process.env.DOMAIN_ROOT ?? 'somossitu.com').replace(/^https?:\/\//, '').replace(/\/$/, '');
const supportEmail = `soporte@${domainRoot}`;
const deleteAccountUrl = `https://app.${domainRoot}/cuenta/eliminar`;

export default function ContactPage() {
  return (
    <section className="mx-auto w-full max-w-xl px-5 py-12">
      <h1 className="font-display text-5xl">Contacto</h1>
      <p className="mt-3 text-gray600">Escríbenos si no encontraste la respuesta en los artículos.</p>
      <p className="mt-4 text-gray600">
        Correo: <a className="font-semibold text-ink underline-offset-2 hover:underline" href={`mailto:${supportEmail}`}>{supportEmail}</a>
      </p>
      <p className="mt-2 text-gray600">Respondemos en 2 días hábiles. Si pides el retiro de un contenido, el plazo es de 15 días.</p>
      <p className="mt-2 text-gray600">
        Para eliminar tu cuenta, usa la app o la <a className="font-semibold text-ink underline-offset-2 hover:underline" href={deleteAccountUrl}>página de eliminación</a>.
      </p>
      <div className="mt-8">
        <ContactForm />
      </div>
    </section>
  );
}
