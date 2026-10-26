import type { EnableStaffTwoFactorResponse } from '@/lib/api-contract';
import { enrollCookie } from '@/lib/cookies';
import { writeSession } from '@/lib/session';
import { clientForwarding, upstream } from '@/lib/upstream';
import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { publicProblem, rejectCrossSite } from '@/lib/csrf';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  const token = (await cookies()).get(enrollCookie)?.value;
  if (!token) {
    return NextResponse.json({ detail: 'La inscripción ya no es válida. Vuelve a ingresar.' }, { status: 401 });
  }

  let code = '';
  try {
    const body = (await request.json()) as { code?: string };
    code = typeof body.code === 'string' ? body.code : '';
  } catch {
    return NextResponse.json({ detail: 'No pudimos leer la solicitud.' }, { status: 400 });
  }

  const response = await upstream('admin/2fa/enable', {
    method: 'POST',
    token,
    body: JSON.stringify({ code }),
    contentType: 'application/json',
    forwarded: clientForwarding(request),
  });
  if (response.status !== 200) {
    return NextResponse.json(publicProblem(response.text), { status: response.status });
  }

  const payload = JSON.parse(response.text) as Partial<EnableStaffTwoFactorResponse>;
  if (!payload.accessToken || !payload.refreshToken || !payload.recoveryCodes) {
    return NextResponse.json({ detail: 'No pudimos completar la solicitud.' }, { status: 502 });
  }

  await writeSession(payload.accessToken, payload.refreshToken);
  return NextResponse.json({ recoveryCodes: payload.recoveryCodes });
}
