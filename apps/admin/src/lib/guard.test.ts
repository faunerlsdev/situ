import { classifyStatus, csrfAccepts, idleMs, refreshMs, sessionCookieOptions, sessionDecision } from '@/lib/guard';
import { loginView } from '@/lib/login-view';
import { describe, expect, it } from 'vitest';

describe('panel session', () => {
  it('closes the session after 30 minutes without activity and refreshes the token after 4', () => {
    const now = 1_000_000;
    expect(sessionDecision(now, null, null)).toBe('anonymous');
    expect(sessionDecision(now, now - idleMs, now)).toBe('idle');
    expect(sessionDecision(now, now - 1000, now - refreshMs)).toBe('refresh');
    expect(sessionDecision(now, now - 1000, now - 1000)).toBe('ok');
  });

  it('keeps tokens in httpOnly strict cookies and rejects a mutation without CSRF', () => {
    expect(sessionCookieOptions(true)).toMatchObject({ httpOnly: true, secure: true, sameSite: 'strict' });
    expect(sessionCookieOptions(false).secure).toBe(false);
    expect(csrfAccepts('POST', 'http://localhost:3001', 'localhost:3001', 'token', 'token')).toBe(true);
    expect(csrfAccepts('POST', 'http://localhost:3001', 'localhost:3001', 'token', 'otro')).toBe(false);
    expect(csrfAccepts('POST', 'https://otro.example', 'localhost:3001', 'token', 'token')).toBe(false);
    expect(csrfAccepts('GET', null, null, undefined, null)).toBe(true);
  });

  it('maps 401 and 403 and never returns the access token to the browser', () => {
    expect(classifyStatus(401)).toBe('session');
    expect(classifyStatus(403)).toBe('forbidden');
    expect(classifyStatus(400)).toBe('other');
    const view = loginView({ mfaRequired: true, mfaToken: 'secreto', accessToken: 'no-debe-salir' });
    expect(view).toEqual({ step: 'mfa' });
    expect(JSON.stringify(view)).not.toContain('secreto');
    expect(loginView({ accessToken: 'a', refreshToken: 'b' })).toEqual({ step: 'session' });
  });
});
