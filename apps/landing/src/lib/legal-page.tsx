import type { Metadata } from 'next';
import { LegalArticle } from '@/components/legal-article';
import { loadLegal } from '@/lib/load-legal';
import { publicSiteUrls } from '@/lib/legal-document';

export async function legalMetadata(slug: string, pathName: string): Promise<Metadata> {
  const document = await loadLegal(slug);
  const canonical = `${publicSiteUrls().landing}${pathName}`;
  return {
    title: document.title,
    description: document.description,
    alternates: { canonical },
    robots: { index: true, follow: true },
  };
}

export async function LegalPage({ slug }: { slug: string }) {
  const document = await loadLegal(slug);
  return <LegalArticle document={document} />;
}
