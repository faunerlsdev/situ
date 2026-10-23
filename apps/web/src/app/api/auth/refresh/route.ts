import { refreshFromCookie } from '@/lib/refresh-session';
import { NextResponse } from 'next/server';

export async function POST() {
  const refreshed = await refreshFromCookie();
  if (!refreshed) {
    return NextResponse.json({ detail: 'La sesión ya no es válida. Vuelve a ingresar.' }, { status: 401 });
  }

  return NextResponse.json({ ok: true });
}
