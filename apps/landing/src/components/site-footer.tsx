import { sites } from '@situ/shared';

export function SiteFooter() {
  return (
    <footer className="border-t border-gray200 bg-ink text-white">
      <div className="mx-auto grid w-full max-w-6xl gap-8 px-5 py-10 sm:grid-cols-[1.4fr_1fr]">
        <div>
          <p className="font-display text-2xl">SITU</p>
          <p className="mt-3 max-w-md text-sm leading-6 text-white/80">
            Hecho para Colombia. Los reportes viven en el mapa; la transmisión en vivo solo empieza en la app.
          </p>
        </div>
        <nav aria-label="Legal">
          <ul className="space-y-2 text-sm">
            <li>
              <a href="/privacidad" className="underline-offset-2 hover:underline">
                Privacidad
              </a>
            </li>
            <li>
              <a href="/terminos" className="underline-offset-2 hover:underline">
                Términos
              </a>
            </li>
            <li>
              <a href={`${sites.support}/solicitudes`} className="underline-offset-2 hover:underline">
                Habeas data y retiro de contenido
              </a>
            </li>
            <li>
              <a href={`${sites.support}/contacto`} className="underline-offset-2 hover:underline">
                Contacto
              </a>
            </li>
          </ul>
        </nav>
      </div>
      <p className="border-t border-white/15 px-5 py-4 text-center text-xs text-white/70">© 2026 SITU</p>
    </footer>
  );
}
