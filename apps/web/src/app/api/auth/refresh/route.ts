import { rejectCrossSite } from '@/lib/csrf';
import { refreshFromCookie } from '@/lib/refresh-session';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  const refreshed = await refreshFromCookie();
  if (!refreshed) {
    return NextResponse.json({ detail: 'La sesión ya no es válida. Vuelve a ingresar.' }, { status: 401 });
  }

  return NextResponse.json({ ok: true });
}
