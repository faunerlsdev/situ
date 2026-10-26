import axios from 'axios';
import type { AxiosAdapter, AxiosInstance, InternalAxiosRequestConfig } from 'axios';

import { httpTimeouts } from '../config/env';
import { AppError, isAppError } from './errors';
import { toAppError, shouldRetry } from './normalize';
import { reportError } from './reportError';
import { createRefreshGate } from './refreshGate';

declare module 'axios' {
  export interface AxiosRequestConfig {
    skipAuthRefresh?: boolean;
    requiresAuth?: boolean;
    retryCount?: number;
  }

  export interface InternalAxiosRequestConfig {
    skipAuthRefresh?: boolean;
    requiresAuth?: boolean;
    retryCount?: number;
  }
}

export type HttpRequest = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  url: string;
  data?: unknown;
  headers?: Record<string, string>;
  timeoutMs?: number;
  skipAuthRefresh?: boolean;
  requiresAuth?: boolean;
  signal?: AbortSignal;
  onUploadProgress?: (percent: number) => void;
  baseURL?: string;
  validateStatus?: (status: number) => boolean;
};

export type HttpResponse<T> = {
  data: T | undefined;
  status: number;
  headers: Record<string, string>;
};

export type HttpClient = {
  request<T>(request: HttpRequest): Promise<T>;
  requestResponse<T>(request: HttpRequest): Promise<HttpResponse<T>>;
};

export type HttpClientOptions = {
  baseUrl: string;
  timeoutMs?: number;
  uploadTimeoutMs?: number;
  getAccessToken?: () => string | null;
  prepareRequest?: () => Promise<void>;
  refreshAccessToken?: () => Promise<string | null>;
  onSessionExpired?: () => void;
  adapter?: AxiosAdapter;
};

export function createHttpClient(options: HttpClientOptions): HttpClient {
  const instance = axios.create({
    baseURL: options.baseUrl,
    timeout: options.timeoutMs ?? httpTimeouts.defaultMs,
    adapter: options.adapter,
    headers: { Accept: 'application/json' },
  });
  const refreshOnce = createRefreshGate(() => options.refreshAccessToken?.() ?? Promise.resolve(null));
  attachInterceptors(instance, options, refreshOnce);

  return {
    async request<T>(request: HttpRequest): Promise<T> {
      const response = await execute<T>(instance, request);
      return response.data as T;
    },
    requestResponse<T>(request: HttpRequest) {
      return execute<T>(instance, request);
    },
  };
}

async function execute<T>(instance: AxiosInstance, request: HttpRequest): Promise<HttpResponse<T>> {
  const response = await instance.request<T>({
    url: request.url,
    method: request.method ?? 'GET',
    data: request.data,
    headers: request.headers,
    timeout: request.timeoutMs,
    signal: request.signal,
    baseURL: request.baseURL,
    skipAuthRefresh: request.skipAuthRefresh,
    requiresAuth: request.requiresAuth,
    // Axios drops its default validator when the key is present with value undefined,
    // and then treats 4xx/5xx as success. Only send the key when the caller set one.
    ...(request.validateStatus ? { validateStatus: request.validateStatus } : {}),
    onUploadProgress: request.onUploadProgress
      ? (event) => {
          if (event.total && event.total > 0) {
            request.onUploadProgress?.(Math.round((event.loaded / event.total) * 100));
          }
        }
      : undefined,
  });
  const headers: Record<string, string> = {};
  for (const [key, value] of Object.entries(response.headers)) {
    if (typeof value === 'string') {
      headers[key.toLowerCase()] = value;
    }
  }

  if (response.status === 204 || response.status === 304 || response.data === '' || response.data === undefined) {
    return { data: undefined, status: response.status, headers };
  }

  return { data: response.data, status: response.status, headers };
}

function attachInterceptors(
  instance: AxiosInstance,
  options: HttpClientOptions,
  refreshOnce: () => Promise<string | null>,
) {
  instance.interceptors.request.use(async (config) => {
    if (!config.skipAuthRefresh) {
      await options.prepareRequest?.();
    }

    const token = config.skipAuthRefresh ? null : options.getAccessToken?.();
    if (token) {
      config.headers.set('Authorization', `Bearer ${token}`);
    }

    if (isMultipart(config.data)) {
      config.headers.delete('Content-Type');
    } else if (config.data !== undefined && !config.headers.has('Content-Type')) {
      config.headers.set('Content-Type', 'application/json');
    }

    return config;
  });

  instance.interceptors.response.use(
    (response) => response,
    async (error: unknown) => {
      if (!axios.isAxiosError(error)) {
        const appError = toAppError({ cause: error });
        reportError(appError);
        throw appError;
      }

      const config = error.config as InternalAxiosRequestConfig | undefined;
      const appError = toAppError({
        status: error.response?.status,
        axiosCode: error.code,
        cancelled: axios.isCancel(error),
        data: error.response?.data,
        cause: error,
      });

      if (appError.code === 'CANCELLED') {
        throw appError;
      }

      if (config && (await retryGet(instance, config, appError))) {
        return instance.request(config);
      }

      if (config && error.response?.status === 401 && (await retryAfterRefresh(instance, config, options, refreshOnce))) {
        return instance.request(config);
      }

      if (appError.code === 'SERVER' || appError.code === 'UNKNOWN') {
        reportError(appError);
      }

      throw appError;
    },
  );
}

async function retryGet(instance: AxiosInstance, config: InternalAxiosRequestConfig, error: AppError): Promise<boolean> {
  const attempt = config.retryCount ?? 0;
  if (!shouldRetry(config.method, error.code, attempt)) {
    return false;
  }

  config.retryCount = attempt + 1;
  await delay(200 * 2 ** attempt);
  return true;
}

async function retryAfterRefresh(
  instance: AxiosInstance,
  config: InternalAxiosRequestConfig,
  options: HttpClientOptions,
  refreshOnce: () => Promise<string | null>,
): Promise<boolean> {
  const hadSession = Boolean(config.headers.get('Authorization')) || config.requiresAuth === true;
  if (!hadSession || config.skipAuthRefresh) {
    return false;
  }

  if (options.refreshAccessToken && config.headers.get('Authorization')) {
    const next = await refreshOnce();
    if (next) {
      config.headers.set('Authorization', `Bearer ${next}`);
      config.skipAuthRefresh = true;
      return true;
    }
  }

  if (!options.getAccessToken?.()) {
    options.onSessionExpired?.();
  } else if (!options.refreshAccessToken && config.requiresAuth) {
    options.onSessionExpired?.();
  }

  return false;
}

function isMultipart(data: unknown): boolean {
  if (typeof FormData !== 'undefined' && data instanceof FormData) {
    return true;
  }

  return typeof data === 'object' && data !== null && Array.isArray((data as { _parts?: unknown })._parts);
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

export { isAppError };
