import type { Metadata } from 'next';
import { PrivacyArticle } from '@/components/privacy-article';
import { publicSiteUrls } from '@/lib/legal-document';
import { loadTerms } from '@/lib/terms-policy';

export const dynamic = 'force-static';

export async function generateMetadata(): Promise<Metadata> {
  const canonical = `${publicSiteUrls().landing}/terminos`;
  return {
    title: 'Términos y condiciones',
    description: 'Términos y condiciones de uso de SITU.',
    alternates: { canonical },
    robots: { index: true, follow: true },
  };
}

export default async function TermsPage() {
  const document = await loadTerms();
  return <PrivacyArticle document={document} />;
}
