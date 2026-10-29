import { sites } from '@situ/shared';

export function SiteFooter() {
  return (
    <footer className="mt-auto border-t border-gray200 bg-white">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 px-5 py-6 text-sm text-gray600 sm:flex-row sm:justify-between">
        <p>Centro de ayuda de SITU. © 2026</p>
        <nav aria-label="Legal">
          <ul className="flex gap-4">
            <li>
              <a href={`${sites.landing}/privacidad`} className="font-semibold text-ink underline-offset-2 hover:underline">
                Privacidad
              </a>
            </li>
            <li>
              <a href={`${sites.landing}/terminos`} className="font-semibold text-ink underline-offset-2 hover:underline">
                Términos
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </footer>
  );
}
