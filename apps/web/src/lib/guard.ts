export const refreshAfterMs = 12 * 60 * 1000;

export type SessionDecision = 'anonymous' | 'refresh' | 'ok';

export function sessionDecision(now: number, issuedAt: number | null, hasRefresh: boolean): SessionDecision {
  if (issuedAt == null) {
    return hasRefresh ? 'refresh' : 'anonymous';
  }

  if (now - issuedAt >= refreshAfterMs) {
    return 'refresh';
  }

  return 'ok';
}

export function csrfAccepts(method: string, origin: string | null, host: string | null, cookie: string | undefined, header: string | null) {
  if (method === 'GET' || method === 'HEAD' || method === 'OPTIONS') {
    return true;
  }

  if (!origin || !host || !cookie || !header || cookie !== header) {
    return false;
  }

  try {
    return new URL(origin).host === host;
  } catch {
    return false;
  }
}

export function sessionCookieOptions(secure: boolean) {
  return {
    httpOnly: true,
    secure,
    sameSite: 'lax' as const,
    path: '/',
  };
}

export function classifyStatus(status: number): 'session' | 'forbidden' | 'rate' | 'other' {
  if (status === 401) {
    return 'session';
  }

  if (status === 403) {
    return 'forbidden';
  }

  if (status === 429) {
    return 'rate';
  }

  return 'other';
}
