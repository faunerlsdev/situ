import { clearSession, markSeen, readGate } from '@/lib/session';
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

  await markSeen();
  return NextResponse.json({ ok: true });
}
