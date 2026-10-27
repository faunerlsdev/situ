import { describe, expect, it } from 'vitest';
import { classifyStatus, csrfAccepts, sessionCookieOptions, sessionDecision } from './guard';

describe('sesión web', () => {
  it('renueva el access token antes de que venza y no cierra por inactividad', () => {
    const now = 1_000_000;
    expect(sessionDecision(now, null, false)).toBe('anonymous');
    expect(sessionDecision(now, null, true)).toBe('refresh');
    expect(sessionDecision(now, now - 60_000, true)).toBe('ok');
    expect(sessionDecision(now, now - 13 * 60 * 1000, true)).toBe('refresh');
  });

  it('exige el mismo token CSRF y el mismo origen', () => {
    expect(csrfAccepts('GET', null, null, undefined, null)).toBe(true);
    expect(csrfAccepts('POST', 'http://localhost:3002', 'localhost:3002', 'token', 'token')).toBe(true);
    expect(csrfAccepts('POST', 'http://localhost:3002', 'localhost:3002', 'token', 'otro')).toBe(false);
    expect(csrfAccepts('POST', 'https://otro.example', 'localhost:3002', 'token', 'token')).toBe(false);
  });

  it('deja las cookies httpOnly, Strict y Secure fuera de local', () => {
    expect(sessionCookieOptions(true)).toEqual({ httpOnly: true, secure: true, sameSite: 'strict', path: '/' });
    expect(sessionCookieOptions(false).secure).toBe(false);
  });

  it('distingue sesión, prohibido y límite', () => {
    expect(classifyStatus(401)).toBe('session');
    expect(classifyStatus(403)).toBe('forbidden');
    expect(classifyStatus(429)).toBe('rate');
    expect(classifyStatus(500)).toBe('other');
  });
});
