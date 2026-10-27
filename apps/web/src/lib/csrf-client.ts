import { csrfCookie } from '@/lib/cookies';

export function csrfHeaders(): Record<string, string> {
  if (typeof document === 'undefined') {
    return {};
  }

  const row = document.cookie.split('; ').find((item) => item.startsWith(`${csrfCookie}=`));
  const token = row ? decodeURIComponent(row.slice(csrfCookie.length + 1)) : '';
  return token ? { 'x-csrf-token': token } : {};
}
