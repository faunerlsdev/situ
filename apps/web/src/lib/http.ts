import { createHttpClient, createServices, defaultApiUrl, readApiUrl, sessionEvents } from '@situ/shared';
import type { HttpRequest } from '@situ/shared';
import { csrfHeaders } from '@/lib/csrf-client';

export const http = createHttpClient({
  baseUrl: readApiUrl(process.env.NEXT_PUBLIC_API_URL, defaultApiUrl),
  getAccessToken: () => null,
  onSessionExpired: () => {
    if (typeof window !== 'undefined') {
      sessionEvents.emitExpired();
    }
  },
});

function browserRequest(input: HttpRequest): HttpRequest {
  const onServer = typeof window === 'undefined';
  return {
    ...input,
    url: onServer ? input.url : input.url.replace('/api/v1/', '/api/bff/'),
    baseURL: onServer ? undefined : '',
    headers: { ...input.headers, ...(onServer ? {} : csrfHeaders()) },
    skipAuthRefresh: onServer ? input.skipAuthRefresh : true,
  };
}

export const api = createServices({
  request: (input) => http.request(browserRequest(input)),
  requestResponse: (input) => http.requestResponse(browserRequest(input)),
});

export async function readBrowserSession() {
  const response = await http.requestResponse<{ signedIn: boolean }>({
    url: '/api/auth/session',
    baseURL: '',
    skipAuthRefresh: true,
    validateStatus: () => true,
  });
  return response.data ?? { signedIn: false };
}

export function postBff<T>(url: string, data?: unknown): Promise<T> {
  return http.request<T>({
    method: 'POST',
    url,
    data,
    baseURL: '',
    headers: csrfHeaders(),
    skipAuthRefresh: true,
  });
}
