import type { Metadata } from 'next';
import { DeleteAccountPanel } from '@/features/account/delete-account-panel';

export const metadata: Metadata = {
  title: 'Eliminar cuenta',
  robots: { index: false, follow: false },
};

export default function DeleteAccountPage() {
  return <DeleteAccountPanel />;
}
