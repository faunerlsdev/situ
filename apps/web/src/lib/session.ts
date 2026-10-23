import { cookies } from 'next/headers';

export const accessCookie = 'situ_access';
export const refreshCookie = 'situ_refresh';

const accessMaxAge = 60 * 15;
const refreshMaxAge = 60 * 60 * 24 * 30;

function base() {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax' as const,
  };
}

export async function writeSession(accessToken: string, refreshToken: string) {
  const jar = await cookies();
  jar.set(accessCookie, accessToken, { ...base(), path: '/', maxAge: accessMaxAge });
  jar.set(refreshCookie, refreshToken, { ...base(), path: '/api/auth', maxAge: refreshMaxAge });
}

export async function clearSession() {
  const jar = await cookies();
  jar.set(accessCookie, '', { ...base(), path: '/', maxAge: 0 });
  jar.set(refreshCookie, '', { ...base(), path: '/api/auth', maxAge: 0 });
}

export async function readSessionTokens() {
  const jar = await cookies();
  return {
    accessToken: jar.get(accessCookie)?.value,
    refreshToken: jar.get(refreshCookie)?.value,
  };
}
