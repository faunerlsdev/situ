import type { Metadata } from 'next';
import { ContactForm } from '@/components/contact-form';

export const metadata: Metadata = { title: 'Contacto' };

export default function ContactPage() {
  return (
    <section className="mx-auto w-full max-w-xl px-5 py-12">
      <h1 className="font-display text-5xl">Contacto</h1>
      <p className="mt-3 text-gray600">Escríbenos si no encontraste la respuesta en los artículos.</p>
      <div className="mt-8">
        <ContactForm />
      </div>
    </section>
  );
}
