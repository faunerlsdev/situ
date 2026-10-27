import { AppError } from '@situ/shared';
import { http } from '@/lib/http';
import { clearSession, readSessionTokens, writeSession } from '@/lib/session';

export async function refreshFromCookie() {
  const { refreshToken } = await readSessionTokens();
  if (!refreshToken) {
    return false;
  }

  try {
    const session = await http.request<{ accessToken: string; refreshToken: string; refreshTokenExpiresAt?: string }>({
      method: 'POST',
      url: '/api/v1/auth/refresh',
      data: { refreshToken },
      skipAuthRefresh: true,
    });
    await writeSession(session.accessToken, session.refreshToken, session.refreshTokenExpiresAt);
    return true;
  } catch (cause) {
    if (cause instanceof AppError) {
      await clearSession();
      return false;
    }

    throw cause;
  }
}
