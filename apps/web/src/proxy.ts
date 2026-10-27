import { csrfCookie, deviceCookie } from '@/lib/cookies';
import { NextResponse, type NextRequest } from 'next/server';

function apiSources() {
  const raw = process.env.NEXT_PUBLIC_API_URL ?? 'http://127.0.0.1:5080';
  try {
    const url = new URL(raw);
    const ws = `${url.protocol === 'https:' ? 'wss:' : 'ws:'}//${url.host}`;
    return { http: url.origin, ws };
  } catch {
    return { http: 'http://127.0.0.1:5080', ws: 'ws://127.0.0.1:5080' };
  }
}

export function proxy(request: NextRequest) {
  const nonce = btoa(crypto.randomUUID());
  const devEval = process.env.NODE_ENV === 'production' ? '' : " 'unsafe-eval'";
  const api = apiSources();
  const csp = [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' https://maps.googleapis.com https://maps.gstatic.com${devEval}`,
    "style-src 'self' 'unsafe-inline'",
    `img-src 'self' data: blob: https://maps.gstatic.com https://maps.googleapis.com https://*.gstatic.com https://*.googleapis.com https://*.ggpht.com ${api.http}`,
    `media-src 'self' blob: ${api.http}`,
    "font-src 'self' https://fonts.gstatic.com",
    `connect-src 'self' ${api.http} ${api.ws} https://maps.googleapis.com https://maps.gstatic.com`,
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
  response.headers.set('X-Frame-Options', 'DENY');
  response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
  response.headers.set('Permissions-Policy', 'camera=(), microphone=(), geolocation=(self), payment=()');
  response.headers.set('X-Content-Type-Options', 'nosniff');
  const secure = process.env.NODE_ENV === 'production';
  if (!request.cookies.get(csrfCookie)?.value) {
    response.cookies.set(csrfCookie, crypto.randomUUID(), {
      httpOnly: false,
      secure,
      sameSite: 'strict',
      path: '/',
    });
  }

  if (!request.cookies.get(deviceCookie)?.value) {
    response.cookies.set(deviceCookie, crypto.randomUUID(), {
      httpOnly: true,
      secure,
      sameSite: 'strict',
      path: '/',
      maxAge: 60 * 60 * 24 * 400,
    });
  }

  return response;
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
