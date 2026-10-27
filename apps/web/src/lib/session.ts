import { refreshCookieMaxAge } from '@situ/shared';
import { cookies } from 'next/headers';
import { accessCookie, csrfCookie, deviceCookie, guardianCookie, issuedCookie, refreshCookie } from '@/lib/cookies';
import { sessionCookieOptions, sessionDecision } from '@/lib/guard';

const accessMaxAge = 60 * 15;
const deviceMaxAge = 60 * 60 * 24 * 400;

function secure() {
  return process.env.NODE_ENV === 'production';
}

export async function writeSession(accessToken: string, refreshToken: string, refreshExpiresAt?: string) {
  const jar = await cookies();
  const base = sessionCookieOptions(secure());
  const refreshMaxAge = refreshCookieMaxAge(refreshExpiresAt, Date.now());
  jar.set(accessCookie, accessToken, { ...base, maxAge: accessMaxAge });
  jar.set(refreshCookie, refreshToken, { ...base, path: '/api', maxAge: refreshMaxAge });
  jar.set(issuedCookie, Date.now().toString(), { ...base, maxAge: refreshMaxAge });
  jar.set(guardianCookie, '', { ...base, path: '/api', maxAge: 0 });
}

export async function writeGuardian(accessToken: string) {
  const jar = await cookies();
  jar.set(guardianCookie, accessToken, { ...sessionCookieOptions(secure()), path: '/api', maxAge: accessMaxAge });
}

export async function clearSession() {
  const jar = await cookies();
  const base = sessionCookieOptions(secure());
  for (const name of [accessCookie, issuedCookie]) {
    jar.set(name, '', { ...base, maxAge: 0 });
  }
  jar.set(refreshCookie, '', { ...base, path: '/api', maxAge: 0 });
  jar.set(guardianCookie, '', { ...base, path: '/api', maxAge: 0 });
}

export async function readSessionTokens() {
  const jar = await cookies();
  return {
    accessToken: jar.get(accessCookie)?.value,
    refreshToken: jar.get(refreshCookie)?.value,
    guardianToken: jar.get(guardianCookie)?.value,
  };
}

export async function readGate() {
  const jar = await cookies();
  const issued = Number(jar.get(issuedCookie)?.value);
  const hasRefresh = Boolean(jar.get(refreshCookie)?.value);
  return sessionDecision(Date.now(), Number.isFinite(issued) && issued > 0 ? issued : null, hasRefresh);
}

export async function bearerToken() {
  const jar = await cookies();
  return jar.get(accessCookie)?.value ?? jar.get(guardianCookie)?.value;
}

export async function deviceId() {
  const jar = await cookies();
  const existing = jar.get(deviceCookie)?.value;
  if (existing && /^[0-9a-f-]{36}$/i.test(existing)) {
    return existing;
  }

  const created = crypto.randomUUID();
  jar.set(deviceCookie, created, { ...sessionCookieOptions(secure()), maxAge: deviceMaxAge });
  return created;
}

export { csrfCookie };
