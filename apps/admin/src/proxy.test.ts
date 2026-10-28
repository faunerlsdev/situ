import { csrfCookie } from '@/lib/cookies';
import { NextRequest } from 'next/server';
import { describe, expect, it } from 'vitest';
import { config, proxy } from './proxy';

describe('admin proxy', () => {
  it('keeps the security headers, the nonce and the csrf cookie', () => {
    const response = proxy(new NextRequest('http://localhost:3001/denuncias'));
    const csp = response.headers.get('Content-Security-Policy') ?? '';
    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("'strict-dynamic'");
    expect(csp).toContain(process.env.NODE_ENV === 'production' ? "script-src 'self'" : "'unsafe-eval'");
    expect(csp).not.toMatch(/script-src[^;]*'unsafe-inline'/);
    expect(response.headers.get('Cache-Control')).toBe('private, no-store');
    expect(response.headers.get('X-Frame-Options')).toBe('DENY');
    expect(response.headers.get('Referrer-Policy')).toBe('no-referrer');
    expect(response.headers.get('X-Content-Type-Options')).toBe('nosniff');
    expect(response.headers.get('Permissions-Policy')).toBe('camera=(), microphone=(), geolocation=(), payment=()');
    expect(response.cookies.get(csrfCookie)?.value).toBeTruthy();
    expect(config.matcher).toEqual(['/((?!_next/static|_next/image|favicon.ico).*)']);
  });
});
