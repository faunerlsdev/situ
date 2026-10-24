import { Suspense } from 'react';
import { GuardianManage } from '@/components/guardian-manage';

export default function ManageGuardianPage() {
  return (
    <Suspense fallback={<main className="min-h-dvh bg-[#F7F5F2]" />}>
      <GuardianManage />
    </Suspense>
  );
}
