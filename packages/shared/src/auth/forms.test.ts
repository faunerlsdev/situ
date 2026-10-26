import { describe, expect, it } from 'vitest';

import { registerSchema } from './forms';
import type { RegisterValues } from './forms';

function values(overrides: Partial<RegisterValues> = {}): RegisterValues {
  return {
    givenNames: 'Ana',
    surnames: 'Perez',
    phone: '3001234567',
    userName: 'ana_perez',
    email: 'ana@example.com',
    password: 'Clave123',
    passwordConfirmation: 'Clave123',
    day: '1',
    month: '1',
    year: '1990',
    countryId: 'co',
    regionId: 'reg',
    cityId: 'city',
    accepted: true,
    guardianEmail: '',
    minorNoticeAccepted: false,
    ...overrides,
  };
}

describe('registerSchema guardian fields', () => {
  it('does not ask an adult for a representative', () => {
    expect(registerSchema.safeParse(values()).success).toBe(true);
  });

  it('requires the representative email and the notice when the person is under 18', () => {
    const missing = registerSchema.safeParse(values({ year: '2012' }));
    expect(missing.success).toBe(false);
    if (!missing.success) {
      const paths = missing.error.issues.map((issue) => issue.path[0]);
      expect(paths).toContain('guardianEmail');
      expect(paths).toContain('minorNoticeAccepted');
    }

    const complete = registerSchema.safeParse(
      values({ year: '2012', guardianEmail: 'tutor@example.com', minorNoticeAccepted: true }),
    );
    expect(complete.success).toBe(true);
  });
});
