import { defaultApiUrl } from '@situ/shared';
import { NextResponse } from 'next/server';

export function apiOrigin() {
  return process.env.NEXT_PUBLIC_API_URL ?? defaultApiUrl;
}

export async function forwardProblem(response: Response) {
  const text = await response.text();
  let body: unknown = { detail: 'No pudimos completar la solicitud.' };
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = { detail: text };
    }
  }

  return NextResponse.json(body, { status: response.status });
}

export function isSession(value: unknown): value is { accessToken: string; refreshToken: string } {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const record = value as Record<string, unknown>;
  return typeof record.accessToken === 'string' && record.accessToken.length > 0
    && typeof record.refreshToken === 'string' && record.refreshToken.length > 0;
}
