import { refreshFromCookie } from '@/lib/refresh-session';
import { readSessionTokens } from '@/lib/session';
import { NextResponse } from 'next/server';

export async function GET() {
  const { accessToken, refreshToken } = await readSessionTokens();
  if (accessToken) {
    return NextResponse.json({ signedIn: true });
  }

  if (refreshToken && (await refreshFromCookie())) {
    return NextResponse.json({ signedIn: true });
  }

  return NextResponse.json({ signedIn: false });
}
