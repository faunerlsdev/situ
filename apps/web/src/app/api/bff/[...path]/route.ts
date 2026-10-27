import { rejectCrossSite } from '@/lib/csrf';
import { http } from '@/lib/http';
import { bearerToken, clearSession, readGate, readSessionTokens, writeSession } from '@/lib/session';
import { NextResponse } from 'next/server';

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

async function rotate() {
  const { refreshToken } = await readSessionTokens();
  if (!refreshToken) {
    return false;
  }

  const response = await http.requestResponse<{ accessToken?: string; refreshToken?: string; refreshTokenExpiresAt?: string }>({
    method: 'POST',
    url: '/api/v1/auth/refresh',
    data: { refreshToken },
    skipAuthRefresh: true,
    validateStatus: () => true,
  });
  if (response.status !== 200 || !response.data?.accessToken || !response.data.refreshToken) {
    return false;
  }

  await writeSession(response.data.accessToken, response.data.refreshToken, response.data.refreshTokenExpiresAt);
  return true;
}

async function forward(method: Method, target: string, token: string | undefined, contentType: string | null, body: ArrayBuffer | undefined) {
  return http.requestResponse<unknown>({
    method,
    url: target,
    data: body && body.byteLength > 0 ? body : undefined,
    headers: {
      ...(contentType ? { 'content-type': contentType } : {}),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    skipAuthRefresh: true,
    validateStatus: () => true,
  });
}

async function handle(request: Request, context: { params: Promise<{ path: string[] }> }) {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    const denied = await rejectCrossSite(request);
    if (denied) {
      return denied;
    }
  }

  const gate = await readGate();
  if (gate === 'refresh') {
    const refreshed = await rotate();
    if (!refreshed && !(await bearerToken())) {
      await clearSession();
      return NextResponse.json({ code: 'auth.session_invalid', detail: 'La sesión ya no es válida. Vuelve a ingresar.' }, { status: 401 });
    }
  }

  const { path } = await context.params;
  const url = new URL(request.url);
  const target = `/api/v1/${path.join('/')}${url.search}`;
  const contentType = request.headers.get('content-type');
  const body = request.method === 'GET' || request.method === 'HEAD' ? undefined : await request.arrayBuffer();
  if (request.method !== 'GET' && request.method !== 'POST' && request.method !== 'PUT' && request.method !== 'PATCH' && request.method !== 'DELETE') {
    return NextResponse.json({ detail: 'Método no permitido.' }, { status: 405 });
  }
  const method: Method = request.method;
  let response = await forward(method, target, await bearerToken(), contentType, body);
  if (response.status === 401 && (await rotate())) {
    response = await forward(method, target, await bearerToken(), contentType, body);
  }

  if (response.data === undefined) {
    return new NextResponse(null, { status: response.status });
  }

  const payload = typeof response.data === 'string' ? response.data : JSON.stringify(response.data);
  return new NextResponse(payload, {
    status: response.status,
    headers: { 'content-type': response.headers['content-type'] ?? 'application/json' },
  });
}

export const GET = handle;
export const POST = handle;
export const PUT = handle;
export const PATCH = handle;
export const DELETE = handle;
