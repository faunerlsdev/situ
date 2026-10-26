import type { SetupStaffTwoFactorResponse } from '@/lib/api-contract';
import { enrollCookie } from '@/lib/cookies';
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

  const response = await upstream('admin/2fa/setup', { method: 'POST', token, forwarded: clientForwarding(request) });
  const body = response.status === 200 ? (JSON.parse(response.text) as SetupStaffTwoFactorResponse) : publicProblem(response.text);
  return NextResponse.json(body, { status: response.status });
}
