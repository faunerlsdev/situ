import { NextResponse, type NextRequest } from 'next/server';

function apiOrigin() {
  const raw = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080';
  const origins = new Set();
  try {
    const url = new URL(raw);
    origins.add(url.origin);
    if (url.hostname === 'localhost' || url.hostname === '127.0.0.1') {
      const port = url.port ? `:${url.port}` : '';
      origins.add(`${url.protocol}//localhost${port}`);
      origins.add(`${url.protocol}//127.0.0.1${port}`);
    }
  } catch {
    origins.add('http://localhost:5080');
    origins.add('http://127.0.0.1:5080');
  }
  return [...origins].join(' ');
}

export function proxy(request: NextRequest) {
  const nonce = btoa(crypto.randomUUID());
  const devEval = process.env.NODE_ENV === 'production' ? '' : " 'unsafe-eval'";
  const api = apiOrigin();
  const csp = [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${devEval}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    `connect-src 'self' ${api}`,
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
  response.headers.set('X-Content-Type-Options', 'nosniff');
  return response;
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
