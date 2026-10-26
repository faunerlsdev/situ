import type { LoginBody } from '@/lib/api-contract';
import { enrollCookie, mfaCookie } from '@/lib/cookies';
import { publicProblem, rejectCrossSite } from '@/lib/csrf';
import { loginView, type LoginStepPayload } from '@/lib/login-view';
import { writeLimited, writeSession } from '@/lib/session';
import { clientForwarding, upstream } from '@/lib/upstream';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  let email = '';
  let password = '';
  try {
    const body = (await request.json()) as Partial<LoginBody>;
    email = typeof body.email === 'string' ? body.email : '';
    password = typeof body.password === 'string' ? body.password : '';
  } catch {
    return NextResponse.json({ detail: 'No pudimos leer la solicitud.' }, { status: 400 });
  }

  const response = await upstream('admin/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
    contentType: 'application/json',
    forwarded: clientForwarding(request),
  });
  return answer(response.status, response.text);
}

async function answer(status: number, text: string) {
  if (status !== 200) {
    return NextResponse.json(publicProblem(text), { status });
  }

  const payload = JSON.parse(text) as LoginStepPayload;
  const view = loginView(payload);
  if (view?.step === 'enroll' && payload.enrollmentToken) {
    await writeLimited(enrollCookie, payload.enrollmentToken);
    return NextResponse.json(view);
  }

  if (view?.step === 'mfa' && payload.mfaToken) {
    await writeLimited(mfaCookie, payload.mfaToken);
    return NextResponse.json(view);
  }

  if (view?.step === 'session' && payload.accessToken && payload.refreshToken) {
    await writeSession(payload.accessToken, payload.refreshToken);
    return NextResponse.json(view);
  }

  return NextResponse.json({ detail: 'No pudimos completar la solicitud.' }, { status: 502 });
}
