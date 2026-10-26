import { clearSession, readGate, rotateSession } from '@/lib/session';
import { clientForwarding } from '@/lib/upstream';
import { NextResponse } from 'next/server';
import { rejectCrossSite } from '@/lib/csrf';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  const gate = await readGate();
  if (gate === 'anonymous' || gate === 'idle') {
    await clearSession();
    return NextResponse.json({ code: 'session.idle', detail: 'La sesión se cerró por inactividad. Vuelve a ingresar.' }, { status: 401 });
  }

  const refreshed = await rotateSession(clientForwarding(request));
  if (!refreshed) {
    await clearSession();
    return NextResponse.json({ code: 'auth.session_invalid', detail: 'La sesión ya no es válida. Vuelve a ingresar.' }, { status: 401 });
  }

  return NextResponse.json({ ok: true });
}
