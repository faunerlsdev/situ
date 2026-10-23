import type { Metadata } from 'next';
import { sites } from '@situ/shared';

export const metadata: Metadata = { title: 'Privacidad' };

export default function PrivacyPage() {
  return (
    <article className="mx-auto w-full max-w-3xl px-5 py-16">
      <h1 className="font-display text-5xl">Privacidad</h1>
      <div className="mt-6 space-y-4 leading-8 text-gray600">
        <p>
          SITU trata datos personales en Colombia conforme a la Ley 1581 de 2012. Este texto es un resumen: el aviso
          completo se muestra cuando hace falta tu autorización.
        </p>
        <p>
          Guardamos la cuenta, la ciudad de interés y lo que publicas para mostrar el mapa y avisarte. El autor real de
          un reporte anónimo se conserva para moderación y para cumplir la ley. La antigüedad pública se muestra como
          mes y año, nunca con el día exacto.
        </p>
        <p>
          Puedes pedir acceso, corrección o supresión de tus datos, y también el retiro de un contenido, desde el centro
          de ayuda.
        </p>
      </div>
      <p className="mt-8">
        <a href={`${sites.support}/solicitudes`} className="font-semibold text-ink underline-offset-2 hover:underline">
          Abrir el formulario de habeas data
        </a>
      </p>
    </article>
  );
}
