import { LegalPage, legalMetadata } from '@/lib/legal-page';

export function generateMetadata() {
  return legalMetadata('politica-de-tratamiento-de-datos', '/politica-de-tratamiento-de-datos');
}

export default function TreatmentPage() {
  return <LegalPage slug="politica-de-tratamiento-de-datos" />;
}
