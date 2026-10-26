import { refreshCookie } from '@/lib/cookies';
import { clearSession } from '@/lib/session';
import { clientForwarding, upstream } from '@/lib/upstream';
import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { rejectCrossSite } from '@/lib/csrf';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  const refresh = (await cookies()).get(refreshCookie)?.value;
  if (refresh) {
    await upstream('admin/auth/logout', {
      method: 'POST',
      body: JSON.stringify({ refreshToken: refresh }),
      contentType: 'application/json',
      forwarded: clientForwarding(request),
    });
  }

  await clearSession();
  return NextResponse.json({ ok: true });
}
