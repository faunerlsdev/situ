import { afterEach, describe, expect, it, vi } from 'vitest';
import { clientForwarding, upstream } from './upstream';

describe('client forwarding', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the original browser address and ignores the rest of the chain', () => {
    const request = new Request('https://panel.example/api/bff/denuncias', {
      headers: { 'x-forwarded-for': '203.0.113.8, 10.0.0.4', 'x-forwarded-proto': 'https' },
    });
    expect(clientForwarding(request)).toEqual({ for: '203.0.113.8', proto: 'https' });
  });

  it('does not invent an address when the browser sent none', () => {
    const request = new Request('http://localhost:3001/api/auth/login');
    expect(clientForwarding(request)).toEqual({ for: null, proto: 'http' });
  });

  it('forwards that address to the API', async () => {
    const seen: Headers[] = [];
    vi.stubGlobal('fetch', async (_url: string, init: RequestInit) => {
      seen.push(new Headers(init.headers));
      return new Response('{}', { status: 200 });
    });

    await upstream('admin/auth/login', { method: 'POST', forwarded: { for: '203.0.113.8', proto: 'https' } });
    await upstream('admin/auth/login', { method: 'POST', forwarded: { for: null, proto: 'http' } });

    expect(seen[0]?.get('x-forwarded-for')).toBe('203.0.113.8');
    expect(seen[0]?.get('x-forwarded-proto')).toBe('https');
    expect(seen[1]?.get('x-forwarded-for')).toBeNull();
    expect(seen[1]?.get('x-forwarded-proto')).toBe('http');
  });
});
