import axios from 'axios';
import { describe, expect, it } from 'vitest';

import { loginErrorMessages } from '../feedback/messages';
import { createHttpClient } from './client';
import { errorMessages, messageFor } from './errorMessages';
import { createServices } from './services';

const generic = errorMessages.UNKNOWN;

describe('login error copy', () => {
  it.each(Object.entries(loginErrorMessages))('maps %s to a specific message', (code, message) => {
    const status = code === 'auth.invalid_credentials' || code === 'auth.session_invalid' ? 401 : code === 'auth.locked' ? 423 : code === 'auth.rate_limited' ? 429 : 403;
    const error = messageFor(status === 401 ? 'UNAUTHORIZED' : status === 423 ? 'FORBIDDEN' : status === 429 ? 'RATE_LIMIT' : 'FORBIDDEN', code);
    expect(error).toBe(message);
    expect(error).not.toBe(generic);
    expect(error.length).toBeGreaterThan(0);
  });

  it('keeps the generic text for a code the app does not know', () => {
    expect(messageFor('UNKNOWN')).toBe(generic);
    expect(messageFor('FORBIDDEN', 'auth.not_a_real_code')).toBe(errorMessages.FORBIDDEN);
    expect(errorMessages.FORBIDDEN).not.toBe(generic);
  });
});

describe('restricted sign-in responses', () => {
  it.each(['auth.device_limit', 'auth.guardian_consent_required'] as const)(
    'does not end the session when login returns %s',
    async (code) => {
      let expired = 0;
      const http = createHttpClient({
        baseUrl: 'http://api.test',
        getAccessToken: () => 'limited',
        refreshAccessToken: async () => 'refreshed',
        onSessionExpired: () => {
          expired += 1;
        },
        adapter: (config) =>
          Promise.reject(
            new axios.AxiosError('Forbidden', 'ERR_BAD_REQUEST', config, undefined, {
              data: { code, accessToken: 'limited-token', accessTokenExpiresAt: '2026-10-25T06:00:00Z', detail: 'server text' },
              status: 403,
              statusText: 'Forbidden',
              headers: {},
              config,
            }),
          ),
      });

      await expect(http.request({ method: 'POST', url: '/api/v1/auth/login', skipAuthRefresh: true })).rejects.toMatchObject({
        apiCode: code,
        accessToken: 'limited-token',
        userMessage: loginErrorMessages[code],
      });
      expect(expired).toBe(0);
    },
  );

  it('rejects a problem+json login and keeps the API sentence', async () => {
    const detail = 'Las cuentas del equipo ingresan por el panel de administración.';
    const http = createHttpClient({
      baseUrl: 'http://api.test',
      adapter: (config) => {
        const response = {
          data: { title: 'Forbidden', status: 403, detail, code: 'auth.staff_use_panel' },
          status: 403,
          statusText: 'Forbidden',
          headers: {},
          config,
        };
        const validateStatus = config.validateStatus;
        if (typeof validateStatus !== 'function' || validateStatus(403)) {
          return Promise.resolve(response);
        }

        return Promise.reject(new axios.AxiosError('Forbidden', 'ERR_BAD_REQUEST', config, undefined, response));
      },
    });

    await expect(
      createServices(http).login({
        login: 'equipo',
        password: 'secret-pass-1',
        deviceIdentifier: 'device',
        platform: 'IOS',
      }),
    ).rejects.toMatchObject({
      status: 403,
      apiCode: 'auth.staff_use_panel',
      userMessage: detail,
    });
  });
});
