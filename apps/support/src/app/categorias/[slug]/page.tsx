import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { ArticleSearch } from '@/components/article-search';
import { getCategory, listArticlesByCategory, listCategories } from '@/lib/articles';

type RouteParams = { params: Promise<{ slug: string }> };

export function generateStaticParams() {
  return listCategories().map((category) => ({ slug: category.slug }));
}

export async function generateMetadata({ params }: RouteParams): Promise<Metadata> {
  const { slug } = await params;
  const category = getCategory(slug);
  return { title: category?.title ?? 'Categoría' };
}

export default async function CategoryPage({ params }: RouteParams) {
  const { slug } = await params;
  const category = getCategory(slug);
  if (!category) {
    notFound();
  }

  const articles = listArticlesByCategory(category.slug);

  return (
    <article className="mx-auto w-full max-w-3xl px-5 py-12">
      <p className="text-sm">
        <Link href="/" className="font-semibold text-ink underline-offset-2 hover:underline">
          Volver a la ayuda
        </Link>
      </p>
      <h1 className="mt-4 font-display text-5xl">{category.title}</h1>
      <p className="mt-3 text-gray600">{category.description}</p>
      <div className="mt-8">
        <ArticleSearch articles={articles} />
      </div>
    </article>
  );
}
