import { LegalPage, legalMetadata } from '@/lib/legal-page';

export function generateMetadata() {
  return legalMetadata('contenido-y-moderacion', '/contenido-y-moderacion');
}

export default function ModerationPage() {
  return <LegalPage slug="contenido-y-moderacion" />;
}
