import type { Metadata } from 'next';
import { PrivacyArticle } from '@/components/privacy-article';
import { publicSiteUrls } from '@/lib/legal-document';
import { loadPrivacyPolicy } from '@/lib/privacy-policy';

export const dynamic = 'force-static';

export async function generateMetadata(): Promise<Metadata> {
  const canonical = `${publicSiteUrls().landing}/privacidad`;
  return {
    title: 'Política de privacidad',
    description: 'Política de tratamiento y protección de datos personales de SITU.',
    alternates: { canonical },
    robots: { index: true, follow: true },
  };
}

export default async function PrivacyPage() {
  const document = await loadPrivacyPolicy();
  return <PrivacyArticle document={document} />;
}
