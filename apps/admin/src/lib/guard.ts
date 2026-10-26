export const idleMs = 30 * 60 * 1000;
export const refreshMs = 4 * 60 * 1000;

export type SessionDecision = 'anonymous' | 'idle' | 'refresh' | 'ok';

export function sessionDecision(now: number, seenAt: number | null, issuedAt: number | null): SessionDecision {
  if (seenAt == null) {
    return 'anonymous';
  }

  if (now - seenAt >= idleMs) {
    return 'idle';
  }

  if (issuedAt != null && now - issuedAt >= refreshMs) {
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
    sameSite: 'strict' as const,
    path: '/',
  };
}

export function classifyStatus(status: number): 'session' | 'forbidden' | 'other' {
  if (status === 401) {
    return 'session';
  }

  if (status === 403) {
    return 'forbidden';
  }

  return 'other';
}
