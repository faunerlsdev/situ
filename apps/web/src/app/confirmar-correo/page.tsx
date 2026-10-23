import { ConfirmEmailForm } from '@/components/confirm-email-form';
import { Suspense } from 'react';

export default function ConfirmEmailPage() {
  return (
    <Suspense fallback={<main className="min-h-dvh bg-primary" />}>
      <ConfirmEmailForm />
    </Suspense>
  );
}
