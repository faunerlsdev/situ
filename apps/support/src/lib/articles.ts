import fs from 'node:fs';
import path from 'node:path';

export const categories = [
  {
    slug: 'cuenta',
    title: 'Cuenta',
    description: 'Registro, ingreso y recuperación de la contraseña.',
  },
  {
    slug: 'mapa',
    title: 'Mapa y reportes',
    description: 'Cómo ver, comentar y reportar lo que pasa cerca.',
  },
  {
    slug: 'privacidad',
    title: 'Privacidad y menores',
    description: 'Datos personales, habeas data y protección de menores.',
  },
  {
    slug: 'planes',
    title: 'Planes',
    description: 'Free, Basic, Pro y Advanced.',
  },
] as const;

export type CategorySlug = (typeof categories)[number]['slug'];

export type ArticleSummary = {
  slug: string;
  title: string;
  description: string;
  category: CategorySlug;
};

export type Article = ArticleSummary & {
  body: string;
};

const articlesDirectory = path.join(process.cwd(), 'content', 'articles');

export function listCategories() {
  return categories;
}

export function getCategory(slug: string) {
  return categories.find((category) => category.slug === slug);
}

export function listArticles(): ArticleSummary[] {
  return readArticles()
    .map((article) => ({
      slug: article.slug,
      title: article.title,
      description: article.description,
      category: article.category,
    }))
    .sort((left, right) => left.title.localeCompare(right.title, 'es'));
}

export function listArticlesByCategory(slug: CategorySlug): ArticleSummary[] {
  return listArticles().filter((article) => article.category === slug);
}

export function getArticle(slug: string): Article | undefined {
  if (!/^[a-z0-9-]+$/.test(slug)) {
    return undefined;
  }

  return readArticles().find((article) => article.slug === slug);
}

function readArticles(): Article[] {
  return fs
    .readdirSync(articlesDirectory)
    .filter((file) => file.endsWith('.mdx'))
    .map((file) => {
      const slug = file.slice(0, -'.mdx'.length);
      const source = fs.readFileSync(path.join(articlesDirectory, file), 'utf8');
      return parseArticle(slug, source);
    });
}

function parseArticle(slug: string, source: string): Article {
  const match = /^---\r?\n([\s\S]*?)\r?\n---\r?\n([\s\S]*)$/.exec(source);
  const raw = match?.[1];
  const body = match?.[2];
  if (!raw || body === undefined) {
    throw new Error(`Article ${slug} is missing frontmatter.`);
  }

  const data = new Map<string, string>();
  for (const line of raw.split('\n')) {
    const separator = line.indexOf(':');
    if (separator === -1) {
      continue;
    }

    const key = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim().replace(/^["']|["']$/g, '');
    data.set(key, value);
  }

  const title = data.get('title');
  const description = data.get('description');
  const category = data.get('category');
  if (!title || !description || !isCategory(category)) {
    throw new Error(`Article ${slug} has incomplete frontmatter.`);
  }

  return { slug, title, description, category, body };
}

function isCategory(value: string | undefined): value is CategorySlug {
  return categories.some((category) => category.slug === value);
}
