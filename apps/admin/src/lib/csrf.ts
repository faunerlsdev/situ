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

export function publicProblem(text: string) {
  try {
    const body = JSON.parse(text) as { code?: string; detail?: string; title?: string; retryAfter?: string };
    return { code: body.code, detail: body.detail, title: body.title, retryAfter: body.retryAfter };
  } catch {
    return { detail: 'No pudimos completar la solicitud.' };
  }
}
