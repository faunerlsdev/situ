export type AppErrorCode =
  | 'NETWORK'
  | 'TIMEOUT'
  | 'UNAUTHORIZED'
  | 'FORBIDDEN'
  | 'NOT_FOUND'
  | 'VALIDATION'
  | 'CONFLICT'
  | 'RATE_LIMIT'
  | 'SERVER'
  | 'CANCELLED'
  | 'UNKNOWN';

export class AppError extends Error {
  readonly code: AppErrorCode;
  readonly status?: number;
  readonly fieldErrors?: Record<string, string>;
  readonly userMessage: string;
  readonly apiCode?: string;
  readonly retryAfter?: string;
  readonly accessToken?: string;
  readonly accessTokenExpiresAt?: string;

  constructor(input: {
    code: AppErrorCode;
    userMessage: string;
    status?: number;
    fieldErrors?: Record<string, string>;
    cause?: unknown;
    apiCode?: string;
    retryAfter?: string;
    accessToken?: string;
    accessTokenExpiresAt?: string;
  }) {
    super(input.userMessage, { cause: input.cause });
    this.name = 'AppError';
    this.code = input.code;
    this.userMessage = input.userMessage;
    this.status = input.status;
    this.fieldErrors = input.fieldErrors;
    this.apiCode = input.apiCode;
    this.retryAfter = input.retryAfter;
    this.accessToken = input.accessToken;
    this.accessTokenExpiresAt = input.accessTokenExpiresAt;
  }
}

export function isAppError(error: unknown): error is AppError {
  if (error instanceof AppError) {
    return true;
  }

  if (typeof error !== 'object' || error === null) {
    return false;
  }

  const record = error as { name?: unknown; code?: unknown; userMessage?: unknown };
  return record.name === 'AppError' && typeof record.code === 'string' && typeof record.userMessage === 'string';
}
