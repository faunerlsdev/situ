import type { StaffSessionResponse } from '@/lib/api-contract';
import { mfaCookie } from '@/lib/cookies';
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

  const jar = await cookies();
  const token = jar.get(mfaCookie)?.value;
  if (!token) {
    return NextResponse.json({ detail: 'El código de verificación venció. Vuelve a ingresar.' }, { status: 401 });
  }

  let code: string | undefined;
  let recoveryCode: string | undefined;
  try {
    const body = (await request.json()) as { code?: string; recoveryCode?: string };
    code = typeof body.code === 'string' ? body.code : undefined;
    recoveryCode = typeof body.recoveryCode === 'string' ? body.recoveryCode : undefined;
  } catch {
    return NextResponse.json({ detail: 'No pudimos leer la solicitud.' }, { status: 400 });
  }

  const response = await upstream('admin/auth/mfa', {
    method: 'POST',
    body: JSON.stringify({ mfaToken: token, code, recoveryCode }),
    contentType: 'application/json',
    forwarded: clientForwarding(request),
  });
  if (response.status !== 200) {
    return NextResponse.json(publicProblem(response.text), { status: response.status });
  }

  const session = JSON.parse(response.text) as Partial<StaffSessionResponse>;
  if (!session.accessToken || !session.refreshToken) {
    return NextResponse.json({ detail: 'No pudimos completar la solicitud.' }, { status: 502 });
  }

  await writeSession(session.accessToken, session.refreshToken);
  return NextResponse.json({ step: 'session' });
}
