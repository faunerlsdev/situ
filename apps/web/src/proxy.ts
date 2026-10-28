import { csrfCookie, deviceCookie } from '@/lib/cookies';
import { NextResponse, type NextRequest } from 'next/server';

function apiSources() {
  const raw = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080';
  try {
    const url = new URL(raw);
    const wsProtocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    const http = new Set([url.origin]);
    const ws = new Set([`${wsProtocol}//${url.host}`]);
    if (url.hostname === 'localhost' || url.hostname === '127.0.0.1') {
      const port = url.port ? `:${url.port}` : '';
      http.add(`${url.protocol}//localhost${port}`);
      http.add(`${url.protocol}//127.0.0.1${port}`);
      ws.add(`${wsProtocol}//localhost${port}`);
      ws.add(`${wsProtocol}//127.0.0.1${port}`);
    }
    return { http: [...http].join(' '), ws: [...ws].join(' ') };
  } catch {
    return { http: 'http://localhost:5080 http://127.0.0.1:5080', ws: 'ws://localhost:5080 ws://127.0.0.1:5080' };
  }
}

export function proxy(request: NextRequest) {
  const nonce = btoa(crypto.randomUUID());
  const devEval = process.env.NODE_ENV === 'production' ? '' : " 'unsafe-eval'";
  const api = apiSources();
  const csp = [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' https://maps.googleapis.com https://maps.gstatic.com https://challenges.cloudflare.com${devEval}`,
    "style-src 'self' 'unsafe-inline'",
    `img-src 'self' data: blob: https://maps.gstatic.com https://maps.googleapis.com https://*.gstatic.com https://*.googleapis.com https://*.ggpht.com ${api.http}`,
    `media-src 'self' blob: ${api.http}`,
    "font-src 'self' https://fonts.gstatic.com",
    `connect-src 'self' ${api.http} ${api.ws} https://maps.googleapis.com https://maps.gstatic.com https://challenges.cloudflare.com`,
    "frame-src https://challenges.cloudflare.com",
    "worker-src 'self' blob:",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join('; ');
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set('x-nonce', nonce);
  requestHeaders.set('content-security-policy', csp);
  const response = NextResponse.next({ request: { headers: requestHeaders } });
  response.headers.set('Content-Security-Policy', csp);
  response.headers.set('Cache-Control', 'private, no-store');
  response.headers.set('X-Frame-Options', 'DENY');
  response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
  response.headers.set('Permissions-Policy', 'camera=(), microphone=(), geolocation=(self), payment=()');
  response.headers.set('X-Content-Type-Options', 'nosniff');
  const secure = process.env.NODE_ENV === 'production';
  if (!request.cookies.get(csrfCookie)?.value) {
    response.cookies.set(csrfCookie, crypto.randomUUID(), {
      httpOnly: false,
      secure,
      sameSite: 'lax',
      path: '/',
    });
  }

  if (!request.cookies.get(deviceCookie)?.value) {
    response.cookies.set(deviceCookie, crypto.randomUUID(), {
      httpOnly: true,
      secure,
      sameSite: 'lax',
      path: '/',
      maxAge: 60 * 60 * 24 * 400,
    });
  }

  return response;
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
