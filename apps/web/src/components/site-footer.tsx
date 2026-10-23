import { sites } from '@situ/shared';

export function SiteFooter() {
  return (
    <footer className="mt-auto border-t border-gray200 bg-white">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 px-5 py-6 text-sm text-gray600 sm:flex-row sm:items-center sm:justify-between">
        <p>En la web ves y comentas. La transmisión en vivo solo se inicia en la app.</p>
        <nav aria-label="Sitios de SITU">
          <ul className="flex gap-4">
            <li>
              <a href={sites.landing} className="font-semibold text-ink underline-offset-2 hover:underline">
                Landing
              </a>
            </li>
            <li>
              <a href={sites.support} className="font-semibold text-ink underline-offset-2 hover:underline">
                Ayuda
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </footer>
  );
}
