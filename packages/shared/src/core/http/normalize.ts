import { AppError } from './errors';
import type { AppErrorCode } from './errors';
import { messageFor } from './errorMessages';

export type ErrorInput = {
  status?: number;
  axiosCode?: string;
  cancelled?: boolean;
  data?: unknown;
  cause?: unknown;
};

export function shouldRetry(method: string | undefined, code: AppErrorCode, attempt: number): boolean {
  if (attempt >= 2) {
    return false;
  }

  if ((method ?? 'GET').toUpperCase() !== 'GET') {
    return false;
  }

  return code === 'NETWORK' || code === 'TIMEOUT' || code === 'SERVER';
}

export function toAppError(input: ErrorInput): AppError {
  if (input.cancelled || input.axiosCode === 'ERR_CANCELED') {
    return new AppError({ code: 'CANCELLED', userMessage: messageFor('CANCELLED'), cause: input.cause });
  }

  if (input.axiosCode === 'ECONNABORTED' || input.axiosCode === 'ETIMEDOUT') {
    return new AppError({ code: 'TIMEOUT', userMessage: messageFor('TIMEOUT'), cause: input.cause });
  }

  if (input.status === undefined) {
    return new AppError({ code: 'NETWORK', userMessage: messageFor('NETWORK'), cause: input.cause });
  }

  const body = readBody(input.data);
  const code = category(input.status);
  return new AppError({
    code,
    status: input.status,
    apiCode: body.apiCode,
    retryAfter: body.retryAfter,
    fieldErrors: body.fieldErrors,
    userMessage: messageFor(code, body.apiCode),
    cause: input.cause,
  });
}

function category(status: number): AppErrorCode {
  if (status === 400 || status === 422) {
    return 'VALIDATION';
  }

  if (status === 401) {
    return 'UNAUTHORIZED';
  }

  if (status === 403 || status === 423) {
    return 'FORBIDDEN';
  }

  if (status === 404) {
    return 'NOT_FOUND';
  }

  if (status === 409) {
    return 'CONFLICT';
  }

  if (status === 429) {
    return 'RATE_LIMIT';
  }

  if (status >= 500) {
    return 'SERVER';
  }

  return 'UNKNOWN';
}

function readBody(data: unknown): { apiCode?: string; retryAfter?: string; fieldErrors?: Record<string, string> } {
  if (typeof data !== 'object' || data === null) {
    return {};
  }

  const record = data as Record<string, unknown>;
  const apiCode = typeof record.code === 'string' && record.code.length > 0 ? record.code : undefined;
  const retryAfter = typeof record.retryAfter === 'string' && record.retryAfter.length > 0 ? record.retryAfter : undefined;
  return { apiCode, retryAfter, fieldErrors: readFieldErrors(record.errors) };
}

function readFieldErrors(value: unknown): Record<string, string> | undefined {
  if (typeof value !== 'object' || value === null) {
    return undefined;
  }

  const entries: Record<string, string> = {};
  for (const [key, messages] of Object.entries(value)) {
    const message = Array.isArray(messages) ? messages.find((item) => typeof item === 'string') : undefined;
    if (typeof message === 'string' && message.length > 0) {
      entries[camel(key)] = message;
    }
  }

  return Object.keys(entries).length > 0 ? entries : undefined;
}

function camel(key: string): string {
  return key.length === 0 ? key : key.charAt(0).toLowerCase() + key.slice(1);
}
