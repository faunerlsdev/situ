import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { ArticleBody } from '@/components/article-body';
import { getArticle, getCategory, listArticles } from '@/lib/articles';

type RouteParams = { params: Promise<{ slug: string }> };

export function generateStaticParams() {
  return listArticles().map((article) => ({ slug: article.slug }));
}

export async function generateMetadata({ params }: RouteParams): Promise<Metadata> {
  const { slug } = await params;
  const article = getArticle(slug);
  if (!article) {
    return { title: 'Artículo' };
  }

  return { title: article.title, description: article.description };
}

export default async function ArticlePage({ params }: RouteParams) {
  const { slug } = await params;
  const article = getArticle(slug);
  if (!article) {
    notFound();
  }

  const category = getCategory(article.category);

  return (
    <article className="mx-auto w-full max-w-3xl px-5 py-12">
      <p className="text-sm">
        <Link href="/" className="font-semibold text-ink underline-offset-2 hover:underline">
          Ayuda
        </Link>
        {category ? (
          <>
            {' · '}
            <Link
              href={`/categorias/${category.slug}`}
              className="font-semibold text-ink underline-offset-2 hover:underline"
            >
              {category.title}
            </Link>
          </>
        ) : null}
      </p>
      <h1 className="mt-4 font-display text-5xl">{article.title}</h1>
      <p className="mt-4 text-lg text-gray600">{article.description}</p>
      <ArticleBody source={article.body} />
    </article>
  );
}
