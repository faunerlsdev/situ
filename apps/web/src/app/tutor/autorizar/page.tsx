import { GuardianForm } from '@/components/guardian-form';
import { Suspense } from 'react';

export default function GuardianPage() {
  return (
    <Suspense fallback={<main className="min-h-dvh splash-gradient" />}>
      <GuardianForm />
    </Suspense>
  );
}
