import { rejectCrossSite } from '@/lib/csrf';
import { http } from '@/lib/http';
import { clearSession, readSessionTokens } from '@/lib/session';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  const { refreshToken } = await readSessionTokens();
  if (refreshToken) {
    await http
      .request<void>({
        method: 'POST',
        url: '/api/v1/auth/sign-out',
        data: { refreshToken },
        skipAuthRefresh: true,
      })
      .catch(() => undefined);
  }

  await clearSession();
  return new NextResponse(null, { status: 204 });
}
