import { csrfCookie } from '@/lib/cookies';
import { PanelError, problemMessage, type ProblemBody } from '@/lib/problem';

export function csrfToken() {
  const pair = document.cookie.split('; ').find((item) => item.startsWith(`${csrfCookie}=`));
  return pair ? decodeURIComponent(pair.slice(csrfCookie.length + 1)) : '';
}

export async function panelFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const method = (init.method ?? 'GET').toUpperCase();
  if (method !== 'GET' && method !== 'HEAD' && !headers.has('x-csrf-token')) {
    headers.set('x-csrf-token', csrfToken());
  }

  const response = await fetch(path, { ...init, headers, credentials: 'same-origin' });
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const body = text ? (JSON.parse(text) as ProblemBody & T) : null;
  if (!response.ok) {
    throw new PanelError(response.status, body?.code, problemMessage(body));
  }

  return body as T;
}

export function apiGet<T>(path: string) {
  return panelFetch<T>(`/api/bff/${path}`);
}

export function apiSend<T>(path: string, method: string, data?: unknown) {
  return panelFetch<T>(`/api/bff/${path}`, {
    method,
    headers: data instanceof FormData ? undefined : { 'content-type': 'application/json' },
    body: data instanceof FormData ? data : data == null ? undefined : JSON.stringify(data),
  });
}
