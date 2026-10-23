'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import type { ArticleSummary } from '@/lib/articles';

export function ArticleSearch({ articles }: { articles: ArticleSummary[] }) {
  const [query, setQuery] = useState('');
  const normalized = query.trim().toLocaleLowerCase('es-CO');
  const results = useMemo(
    () =>
      normalized.length === 0
        ? articles
        : articles.filter((article) => {
            const haystack = `${article.title} ${article.description}`.toLocaleLowerCase('es-CO');
            return haystack.includes(normalized);
          }),
    [articles, normalized],
  );

  return (
    <div>
      <label htmlFor="buscar" className="text-sm font-semibold">
        Buscar en la ayuda
      </label>
      <input
        id="buscar"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        type="search"
        placeholder="Cuenta, mapa, habeas data…"
        className="mt-2 w-full rounded-2xl border border-gray200 bg-white px-4 py-3"
      />
      <p className="mt-3 text-sm text-gray600" aria-live="polite">
        {results.length === 1 ? '1 artículo' : `${results.length} artículos`}
      </p>
      {results.length === 0 ? (
        <p className="mt-4 rounded-2xl border border-gray200 bg-white px-4 py-4">No hay artículos con esa búsqueda.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {results.map((article) => (
            <li key={article.slug}>
              <Link href={`/articulos/${article.slug}`} className="block rounded-2xl border border-gray200 bg-white px-4 py-4 hover:border-ink">
                <span className="font-semibold">{article.title}</span>
                <span className="mt-1 block text-sm text-gray600">{article.description}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
