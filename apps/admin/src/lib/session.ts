import { cookies } from 'next/headers';
import { accessCookie, csrfCookie, enrollCookie, issuedCookie, mfaCookie, refreshCookie, seenCookie } from '@/lib/cookies';
import { sessionCookieOptions, sessionDecision } from '@/lib/guard';
import { upstream, type ClientForwarding } from '@/lib/upstream';

export { accessCookie, csrfCookie, enrollCookie, issuedCookie, mfaCookie, refreshCookie, seenCookie } from '@/lib/cookies';

function secure() {
  return process.env.NODE_ENV === 'production';
}

export async function writeSession(accessToken: string, refreshToken: string) {
  const jar = await cookies();
  const now = Date.now().toString();
  const base = sessionCookieOptions(secure());
  jar.set(accessCookie, accessToken, { ...base, maxAge: 60 * 5 });
  jar.set(refreshCookie, refreshToken, { ...base, path: '/api/auth', maxAge: 60 * 30 });
  jar.set(issuedCookie, now, { ...base, maxAge: 60 * 30 });
  jar.set(seenCookie, now, { ...base, maxAge: 60 * 30 });
  jar.set(enrollCookie, '', { ...base, maxAge: 0 });
  jar.set(mfaCookie, '', { ...base, maxAge: 0 });
}

export async function writeLimited(name: typeof enrollCookie | typeof mfaCookie, token: string) {
  const jar = await cookies();
  const base = sessionCookieOptions(secure());
  jar.set(name, token, { ...base, maxAge: 60 * 5 });
  jar.set(accessCookie, '', { ...base, maxAge: 0 });
  jar.set(refreshCookie, '', { ...base, path: '/api/auth', maxAge: 0 });
}

export async function clearSession() {
  const jar = await cookies();
  const base = sessionCookieOptions(secure());
  for (const name of [accessCookie, enrollCookie, mfaCookie, csrfCookie, seenCookie, issuedCookie]) {
    jar.set(name, '', { ...base, maxAge: 0 });
  }

  jar.set(refreshCookie, '', { ...base, path: '/api/auth', maxAge: 0 });
}

export async function readGate() {
  const jar = await cookies();
  const seen = Number(jar.get(seenCookie)?.value);
  const issued = Number(jar.get(issuedCookie)?.value);
  return sessionDecision(Date.now(), Number.isFinite(seen) && seen > 0 ? seen : null, Number.isFinite(issued) && issued > 0 ? issued : null);
}

export async function markSeen() {
  const jar = await cookies();
  jar.set(seenCookie, Date.now().toString(), { ...sessionCookieOptions(secure()), maxAge: 60 * 30 });
}

export async function rotateSession(forwarded?: ClientForwarding) {
  const jar = await cookies();
  const refresh = jar.get(refreshCookie)?.value;
  if (!refresh) {
    return false;
  }

  const response = await upstream('admin/auth/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken: refresh }),
    contentType: 'application/json',
    forwarded,
  });
  if (response.status !== 200) {
    return false;
  }

  const body = JSON.parse(response.text) as { accessToken?: string; refreshToken?: string };
  if (!body.accessToken || !body.refreshToken) {
    return false;
  }

  const base = sessionCookieOptions(secure());
  jar.set(accessCookie, body.accessToken, { ...base, maxAge: 60 * 5 });
  jar.set(refreshCookie, body.refreshToken, { ...base, path: '/api/auth', maxAge: 60 * 30 });
  jar.set(issuedCookie, Date.now().toString(), { ...base, maxAge: 60 * 30 });
  return true;
}

export async function accessToken() {
  const jar = await cookies();
  return jar.get(accessCookie)?.value;
}
