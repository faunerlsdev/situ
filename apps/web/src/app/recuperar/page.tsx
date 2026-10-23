import type { Metadata } from 'next';
import { ResetForm } from '@/components/reset-form';

export const metadata: Metadata = { title: 'Recuperar contraseña' };

export default function RecoverPage() {
  return <ResetForm />;
}
