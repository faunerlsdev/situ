import { AppError } from './errors';

export function reportError(error: unknown): void {
  const appError = error instanceof AppError ? error : undefined;
  const cause = appError?.cause ?? error;
  if (typeof process !== 'undefined' && process.env.NODE_ENV === 'production') {
    void cause;
    return;
  }

  const code = appError?.code ?? 'UNKNOWN';
  if (requestUrl(cause)?.includes('alert-location')) {
    console.error(`[situ] ${code}`);
    return;
  }

  console.error(`[situ] ${code}`, cause);
}

function requestUrl(cause: unknown): string | undefined {
  if (!cause || typeof cause !== 'object' || !('config' in cause)) {
    return undefined;
  }

  const config = cause.config;
  if (!config || typeof config !== 'object' || !('url' in config)) {
    return undefined;
  }

  return typeof config.url === 'string' ? config.url : undefined;
}
