import { AppError } from '@situ/shared';
import { rejectCrossSite } from '@/lib/csrf';
import { http } from '@/lib/http';
import { deviceId, writeGuardian, writeSession } from '@/lib/session';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ detail: 'No pudimos leer la solicitud.' }, { status: 400 });
  }

  const record = typeof body === 'object' && body !== null ? (body as Record<string, unknown>) : {};
  const login = typeof record.login === 'string' ? record.login : '';
  const password = typeof record.password === 'string' ? record.password : '';
  const rememberMe = record.rememberMe === true;

  try {
    const session = await http.request<{ accessToken: string; refreshToken: string; refreshTokenExpiresAt: string }>({
      method: 'POST',
      url: '/api/v1/auth/login',
      data: { login, password, deviceIdentifier: await deviceId(), platform: 'Web', deviceName: 'Web', rememberMe },
      headers: { 'User-Agent': request.headers.get('user-agent') ?? '' },
      skipAuthRefresh: true,
    });
    await writeSession(session.accessToken, session.refreshToken, session.refreshTokenExpiresAt);
    return NextResponse.json({ ok: true });
  } catch (cause) {
    if (cause instanceof AppError && cause.apiCode === 'auth.guardian_consent_required' && cause.accessToken) {
      await writeGuardian(cause.accessToken);
      return NextResponse.json(
        { code: cause.apiCode, detail: cause.userMessage, status: cause.status },
        { status: cause.status ?? 403 },
      );
    }

    if (cause instanceof AppError) {
      return NextResponse.json(
        {
          code: cause.apiCode,
          detail: cause.userMessage,
          retryAfter: cause.retryAfter,
          status: cause.status,
        },
        { status: cause.status ?? 500 },
      );
    }

    return NextResponse.json({ detail: 'No pudimos completar la solicitud.' }, { status: 500 });
  }
}
