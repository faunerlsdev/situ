import Link from 'next/link';
import { ArticleSearch } from '@/components/article-search';
import { listArticles, listCategories } from '@/lib/articles';

export default function HelpHomePage() {
  const articles = listArticles();
  const categories = listCategories();

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-10 px-5 py-12 lg:grid-cols-[18rem_1fr]">
      <aside>
        <h1 className="font-display text-4xl">Ayuda</h1>
        <p className="mt-3 text-sm leading-6 text-gray600">Busca un tema o entra por categoría.</p>
        <nav aria-label="Categorías" className="mt-6">
          <ul className="space-y-2">
            {categories.map((category) => (
              <li key={category.slug}>
                <Link
                  href={`/categorias/${category.slug}`}
                  className="block rounded-2xl border border-gray200 bg-white px-4 py-3 hover:border-ink"
                >
                  <span className="font-semibold">{category.title}</span>
                  <span className="mt-1 block text-sm text-gray600">{category.description}</span>
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </aside>
      <section>
        <h2 className="font-display text-3xl">Artículos</h2>
        <div className="mt-5">
          <ArticleSearch articles={articles} />
        </div>
      </section>
    </div>
  );
}
