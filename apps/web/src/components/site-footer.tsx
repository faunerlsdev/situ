import { sites } from '@situ/shared';

export function SiteFooter() {
  return (
    <footer className="mt-auto border-t border-app-border bg-app-surface">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 px-5 py-6 text-sm text-app-muted sm:flex-row sm:items-center sm:justify-between">
        <p>En la web ves y comentas. La transmisión en vivo solo se inicia en la app.</p>
        <nav aria-label="Sitios de SITU">
          <ul className="flex gap-4">
            <li>
              <a href={sites.landing} className="font-semibold text-app-text underline-offset-2 hover:underline">
                Landing
              </a>
            </li>
            <li>
              <a href={sites.support} className="font-semibold text-app-text underline-offset-2 hover:underline">
                Ayuda
              </a>
            </li>
            <li>
              <a href="/retiro-de-contenido" className="font-semibold text-app-text underline-offset-2 hover:underline">
                Retiro de contenido
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </footer>
  );
}
