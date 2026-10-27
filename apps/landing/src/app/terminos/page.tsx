import { LegalPage, legalMetadata } from '@/lib/legal-page';

export function generateMetadata() {
  return legalMetadata('terminos', '/terminos');
}

export default function TermsPage() {
  return <LegalPage slug="terminos" />;
}
