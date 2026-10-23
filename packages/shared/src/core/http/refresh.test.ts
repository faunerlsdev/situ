import { AxiosError, AxiosHeaders } from 'axios';
import type { AxiosAdapter, InternalAxiosRequestConfig } from 'axios';
import { describe, expect, it } from 'vitest';

import { createHttpClient } from './client';

describe('http refresh', () => {
  it('refreshes once when several requests fail with 401', async () => {
    let refreshes = 0;
    let current = 'old';

    const adapter: AxiosAdapter = (config) => {
      const authorization = AxiosHeaders.from(config.headers).get('Authorization');
      if (authorization === 'Bearer fresh') {
        return Promise.resolve({ data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config });
      }

      return Promise.reject(
        new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config as InternalAxiosRequestConfig, null, {
          data: { code: 'auth.session_invalid' },
          status: 401,
          statusText: 'Unauthorized',
          headers: {},
          config,
        }),
      );
    };

    const http = createHttpClient({
      baseUrl: 'http://api.test',
      adapter,
      getAccessToken: () => current,
      refreshAccessToken: async () => {
        refreshes += 1;
        await new Promise((resolve) => {
          setTimeout(resolve, 15);
        });
        current = 'fresh';
        return 'fresh';
      },
    });

    const [first, second] = await Promise.all([
      http.request<{ ok: boolean }>({ url: '/needs-auth' }),
      http.request<{ ok: boolean }>({ url: '/needs-auth' }),
    ]);

    expect(refreshes).toBe(1);
    expect(first.ok).toBe(true);
    expect(second.ok).toBe(true);
  });
});
