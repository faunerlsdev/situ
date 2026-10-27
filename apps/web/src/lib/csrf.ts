import 'server-only';
import { csrfCookie } from '@/lib/cookies';
import { csrfAccepts } from '@/lib/guard';
import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';

export async function rejectCrossSite(request: Request) {
  const jar = await cookies();
  const ok = csrfAccepts(
    request.method,
    request.headers.get('origin'),
    request.headers.get('host'),
    jar.get(csrfCookie)?.value,
    request.headers.get('x-csrf-token'),
  );
  return ok ? null : NextResponse.json({ code: 'csrf.rejected', detail: 'La solicitud no pasó la verificación de origen.' }, { status: 403 });
}
