import { LegalPage, legalMetadata } from '@/lib/legal-page';

export function generateMetadata() {
  return legalMetadata('privacidad', '/privacidad');
}

export default function PrivacyPage() {
  return <LegalPage slug="privacidad" />;
}
