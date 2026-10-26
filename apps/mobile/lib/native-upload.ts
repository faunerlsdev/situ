import { AppError, messageFor, toAppError } from '@situ/shared';
import { File, UploadType } from 'expo-file-system';

import { apiOrigin, currentAuthHeaders, refreshAuthHeaders } from '@/lib/api';

export async function uploadMultipart<T>(input: {
  uri: string;
  path: string;
  mime: string;
  onProgress?: (percent: number) => void;
  signal?: AbortSignal;
}): Promise<T> {
  const url = `${apiOrigin()}${input.path}`;
  let result = await send(url, input, await currentAuthHeaders());
  if (result.status === 401) {
    const headers = await refreshAuthHeaders();
    if (!headers) {
      throw toAppError({ status: 401 });
    }
    result = await send(url, input, headers);
  }

  if (result.status < 200 || result.status >= 300) {
    throw toAppError({ status: result.status, data: parseBody(result.body) });
  }

  if (result.status === 204 || result.body.trim().length === 0) {
    return undefined as T;
  }

  return parseBody(result.body) as T;
}

async function send(
  url: string,
  input: { uri: string; mime: string; onProgress?: (percent: number) => void; signal?: AbortSignal },
  headers: Record<string, string>,
): Promise<{ status: number; body: string }> {
  try {
    const file = new File(fileUri(input.uri));
    if (!file.exists) {
      throw new AppError({
        code: 'VALIDATION',
        userMessage: 'No pudimos leer esa foto en el teléfono. Tómala de nuevo.',
      });
    }
    return await file.upload(url, {
      httpMethod: 'POST',
      uploadType: UploadType.MULTIPART,
      fieldName: 'file',
      mimeType: input.mime,
      headers,
      sessionType: 'foreground',
      signal: input.signal,
      onProgress: ({ bytesSent, totalBytes }) => {
        if (totalBytes > 0) {
          input.onProgress?.(Math.min(100, Math.round((bytesSent / totalBytes) * 100)));
        }
      },
    });
  } catch (error) {
    if (error instanceof AppError) {
      throw error;
    }
    if (input.signal?.aborted || (error instanceof Error && error.name === 'AbortError')) {
      throw new AppError({ code: 'CANCELLED', userMessage: messageFor('CANCELLED'), cause: error });
    }
    throw toAppError({ cause: error });
  }
}

function fileUri(uri: string): string {
  if (uri.startsWith('file://') || uri.startsWith('content://') || uri.startsWith('ph://') || uri.startsWith('assets-library://')) {
    return uri;
  }
  return uri.startsWith('/') ? `file://${uri}` : uri;
}

function parseBody(body: string): unknown {
  try {
    return JSON.parse(body) as unknown;
  } catch {
    return body;
  }
}
