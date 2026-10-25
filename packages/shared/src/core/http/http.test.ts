import { describe, expect, it } from 'vitest';

import { AppError } from './errors';
import { toAppError } from './normalize';
import { createRefreshGate } from './refreshGate';
import { reportError } from './reportError';

describe('toAppError', () => {
  it('maps each HTTP status to the expected code', () => {
    const cases: Array<[number, AppError['code']]> = [
      [400, 'VALIDATION'],
      [401, 'UNAUTHORIZED'],
      [403, 'FORBIDDEN'],
      [404, 'NOT_FOUND'],
      [409, 'CONFLICT'],
      [422, 'VALIDATION'],
      [423, 'FORBIDDEN'],
      [429, 'RATE_LIMIT'],
      [500, 'SERVER'],
      [418, 'UNKNOWN'],
    ];

    for (const [status, code] of cases) {
      expect(toAppError({ status, data: {} }).code).toBe(code);
    }
  });

  it('uses the curated Spanish copy for a known API code', () => {
    const error = toAppError({ status: 401, data: { code: 'auth.invalid_credentials', detail: 'stack trace' } });
    expect(error.userMessage).toBe('Correo, usuario o contraseña incorrectos.');
    expect(error.userMessage).not.toContain('stack');
  });

  it('reads field errors and retryAfter', () => {
    const error = toAppError({
      status: 400,
      data: { code: 'validation.failed', errors: { Password: ['Muy corta'] }, retryAfter: '2026-10-23T12:00:00Z' },
    });
    expect(error.fieldErrors).toEqual({ password: 'Muy corta' });
    expect(error.retryAfter).toBe('2026-10-23T12:00:00Z');
  });

  it('maps transport failures without showing a status code', () => {
    expect(toAppError({ cancelled: true }).code).toBe('CANCELLED');
    expect(toAppError({ axiosCode: 'ECONNABORTED' }).code).toBe('TIMEOUT');
    expect(toAppError({}).code).toBe('NETWORK');
    expect(toAppError({}).userMessage).not.toMatch(/\d{3}/);
  });
});

describe('createRefreshGate', () => {
  it('runs one refresh for concurrent callers', async () => {
    let calls = 0;
    const gate = createRefreshGate(
      () =>
        new Promise((resolve) => {
          calls += 1;
          setTimeout(() => resolve('next-token'), 20);
        }),
    );

    const tokens = await Promise.all([gate(), gate(), gate()]);
    expect(calls).toBe(1);
    expect(tokens).toEqual(['next-token', 'next-token', 'next-token']);
  });
});

describe('reportError', () => {
  it('does not print coordinates from an alert location failure', () => {
    const lines: string[] = [];
    const original = console.error;
    console.error = (...args: unknown[]) => {
      lines.push(args.map((arg) => JSON.stringify(arg)).join(' '));
    };
    try {
      reportError(new AppError({
        code: 'SERVER',
        userMessage: 'No pudimos completar la solicitud.',
        cause: {
          config: {
            url: '/api/v1/devices/current/alert-location',
            data: { latitude: 4.71, longitude: -74.07, accuracyMeters: 30 },
          },
        },
      }));
    } finally {
      console.error = original;
    }

    const text = lines.join('\n');
    expect(text).toContain('SERVER');
    expect(text).not.toMatch(/latitude|longitude|4\.71|-74\.07/);
  });
});
