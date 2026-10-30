import type { Metadata } from 'next';
import { PrivacyArticle } from '@/components/privacy-article';
import { loadChildSafety } from '@/lib/child-safety';
import { publicSiteUrls } from '@/lib/legal-document';

export const dynamic = 'force-static';

export async function generateMetadata(): Promise<Metadata> {
  const canonical = `${publicSiteUrls().landing}/seguridad-infantil`;
  return {
    title: 'Estándares de seguridad infantil',
    description: 'Estándares de seguridad infantil de SITU y cómo reportar explotación o abuso sexual de menores.',
    alternates: { canonical },
    robots: { index: true, follow: true },
  };
}

export default async function ChildSafetyPage() {
  const { spanish, english } = await loadChildSafety();
  return (
    <>
      <PrivacyArticle document={spanish} extraLinks={[{ href: '#english', label: english.title }]} />
      <PrivacyArticle id="english" document={english} />
    </>
  );
}
