import { AppError } from './errors';

export function reportError(error: unknown): void {
  const appError = error instanceof AppError ? error : undefined;
  const cause = appError?.cause ?? error;
  if (typeof process !== 'undefined' && process.env.NODE_ENV === 'production') {
    void cause;
    return;
  }

  const code = appError?.code ?? 'UNKNOWN';
  console.error(`[situ] ${code}`, cause);
}
