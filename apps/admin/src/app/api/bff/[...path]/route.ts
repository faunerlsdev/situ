import { accessToken, clearSession, markSeen, readGate, rotateSession } from '@/lib/session';
import { clientForwarding, upstream } from '@/lib/upstream';
import { NextResponse } from 'next/server';
import { rejectCrossSite } from '@/lib/csrf';

async function handle(request: Request, context: { params: Promise<{ path: string[] }> }) {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    const denied = await rejectCrossSite(request);
    if (denied) {
      return denied;
    }
  }

  const gate = await readGate();
  if (gate === 'anonymous' || gate === 'idle') {
    await clearSession();
    return NextResponse.json({ code: 'session.idle', detail: 'La sesión se cerró por inactividad. Vuelve a ingresar.' }, { status: 401 });
  }

  const forwarded = clientForwarding(request);
  if (gate === 'refresh') {
    const refreshed = await rotateSession(forwarded);
    if (!refreshed) {
      await clearSession();
      return NextResponse.json({ code: 'auth.session_invalid', detail: 'La sesión ya no es válida. Vuelve a ingresar.' }, { status: 401 });
    }
  }

  await markSeen();
  const { path } = await context.params;
  const url = new URL(request.url);
  const target = `${path.join('/')}${url.search}`;
  const contentType = request.headers.get('content-type');
  const body = request.method === 'GET' || request.method === 'HEAD' ? null : await request.arrayBuffer();
  let response = await upstream(target, { method: request.method, token: await accessToken(), body, contentType, forwarded });
  if (response.status === 401) {
    const refreshed = await rotateSession(forwarded);
    if (refreshed) {
      response = await upstream(target, { method: request.method, token: await accessToken(), body, contentType, forwarded });
    }
  }

  if (!response.text) {
    return new NextResponse(null, { status: response.status });
  }

  return new NextResponse(response.text, {
    status: response.status,
    headers: { 'content-type': response.contentType ?? 'application/json' },
  });
}

export const GET = handle;
export const POST = handle;
export const PUT = handle;
export const DELETE = handle;
