import { createHttpClient, createServices, defaultApiUrl, readApiUrl, sessionEvents } from '@situ/shared';

let accessToken: string | null = null;
let guardianOnly = false;
let deviceLimitOnly = false;
let prepareHandler: (() => Promise<void>) | null = null;
let refreshHandler: (() => Promise<string | null>) | null = null;

export function setAccessToken(token: string | null, options?: { guardian?: boolean; deviceLimit?: boolean }): void {
  accessToken = token;
  guardianOnly = options?.guardian === true && token !== null;
  deviceLimitOnly = options?.deviceLimit === true && token !== null;
}

export function isGuardianSession(): boolean {
  return guardianOnly;
}

export function isDeviceLimitSession(): boolean {
  return deviceLimitOnly;
}

export function setSessionHandlers(handlers: {
  prepareRequest: () => Promise<void>;
  refreshAccessToken: () => Promise<string | null>;
}): void {
  prepareHandler = handlers.prepareRequest;
  refreshHandler = handlers.refreshAccessToken;
}

export const http = createHttpClient({
  baseUrl: readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl),
  getAccessToken: () => accessToken,
  prepareRequest: () => prepareHandler?.() ?? Promise.resolve(),
  refreshAccessToken: () => refreshHandler?.() ?? Promise.resolve(null),
  onSessionExpired: () => sessionEvents.emitExpired(),
});

export const api = createServices(http);
