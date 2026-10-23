import { createApiClient, defaultApiUrl } from '@situ/shared';

export const api = createApiClient({
  baseUrl: process.env.NEXT_PUBLIC_API_URL ?? defaultApiUrl,
});
