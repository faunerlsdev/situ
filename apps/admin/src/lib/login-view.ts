import type { AdminLoginResponse, StaffSessionResponse } from '@/lib/api-contract';

export type LoginStepPayload = Partial<AdminLoginResponse & StaffSessionResponse>;

export function loginView(payload: LoginStepPayload) {
  if (payload.mfaEnrollmentRequired && payload.enrollmentToken) {
    return { step: 'enroll' as const };
  }

  if (payload.mfaRequired && payload.mfaToken) {
    return { step: 'mfa' as const };
  }

  if (payload.accessToken && payload.refreshToken) {
    return { step: 'session' as const };
  }

  return null;
}
