import { NewPasswordForm } from '@/components/new-password-form';
import { Suspense } from 'react';

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<main className="min-h-dvh splash-gradient" />}>
      <NewPasswordForm />
    </Suspense>
  );
}
