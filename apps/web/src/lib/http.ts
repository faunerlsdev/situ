import { createHttpClient, createServices, defaultApiUrl, readApiUrl, sessionEvents } from '@situ/shared';

export const http = createHttpClient({
  baseUrl: readApiUrl(process.env.NEXT_PUBLIC_API_URL, defaultApiUrl),
  onSessionExpired: () => {
    if (typeof window !== 'undefined') {
      sessionEvents.emitExpired();
    }
  },
});

export const api = createServices(http);

export function postBff<T>(url: string, data?: unknown): Promise<T> {
  return http.request<T>({
    method: 'POST',
    url,
    data,
    baseURL: '',
    skipAuthRefresh: true,
  });
}
