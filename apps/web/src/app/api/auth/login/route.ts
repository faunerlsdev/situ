import { AppError } from '@situ/shared';
import { http } from '@/lib/http';
import { writeSession } from '@/lib/session';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ detail: 'No pudimos leer la solicitud.' }, { status: 400 });
  }

  const record = typeof body === 'object' && body !== null ? (body as Record<string, unknown>) : {};
  const login = typeof record.login === 'string' ? record.login : '';
  const password = typeof record.password === 'string' ? record.password : '';
  const deviceIdentifier = typeof record.deviceIdentifier === 'string' ? record.deviceIdentifier : '';

  try {
    const session = await http.request<{ accessToken: string; refreshToken: string }>({
      method: 'POST',
      url: '/api/v1/auth/login',
      data: { login, password, deviceIdentifier, platform: 'Web', deviceName: 'Web' },
      headers: { 'User-Agent': request.headers.get('user-agent') ?? '' },
      skipAuthRefresh: true,
    });
    await writeSession(session.accessToken, session.refreshToken);
    return NextResponse.json({ ok: true });
  } catch (cause) {
    if (cause instanceof AppError) {
      return NextResponse.json(
        {
          code: cause.apiCode,
          detail: cause.userMessage,
          retryAfter: cause.retryAfter,
          status: cause.status,
          accessToken: cause.accessToken,
          accessTokenExpiresAt: cause.accessTokenExpiresAt,
        },
        { status: cause.status ?? 500 },
      );
    }

    return NextResponse.json({ detail: 'No pudimos completar la solicitud.' }, { status: 500 });
  }
}
