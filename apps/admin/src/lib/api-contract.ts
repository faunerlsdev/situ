import type { components } from '@situ/shared/schema';

export type LoginBody = components['schemas']['LoginBody'];
export type AdminLoginResponse = components['schemas']['AdminLoginResponse'];
export type SetupStaffTwoFactorResponse = components['schemas']['SetupStaffTwoFactorResponse'];
export type EnableStaffTwoFactorResponse = components['schemas']['EnableStaffTwoFactorResponse'];
export type StaffSessionResponse = components['schemas']['StaffSessionResponse'];
export type AdminMe = components['schemas']['GetAdminMeResponse'];
export type Dashboard = components['schemas']['GetAdminDashboardResponse'];

export function count(value: number | string) {
  return Number(value);
}
