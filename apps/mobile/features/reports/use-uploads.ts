import { AppError, messageFor, nextQueued, reduceUploads, reportMediaFallbacks } from '@situ/shared';
import type { UploadTask } from '@situ/shared';
import { manipulateAsync, SaveFormat } from 'expo-image-manipulator';
import { useCallback, useRef, useState } from 'react';

import { api } from '@/lib/api';

export type LocalUpload = UploadTask & {
  uri: string;
  name: string;
  mime: string;
};

export function useUploads() {
  const [tasks, setTasks] = useState<LocalUpload[]>([]);
  const tasksRef = useRef(tasks);
  const controllers = useRef(new Map<string, AbortController>());
  const pumping = useRef(false);

  const update = useCallback((next: LocalUpload[]) => {
    tasksRef.current = next;
    setTasks(next);
  }, []);

  const pump = useCallback(async () => {
    if (pumping.current) {
      return;
    }

    const next = nextQueued(tasksRef.current);
    if (!next) {
      return;
    }

    const local = tasksRef.current.find((task) => task.id === next.id);
    if (!local) {
      return;
    }

    pumping.current = true;
    const controller = new AbortController();
    controllers.current.set(local.id, controller);
    update(reduceUploads(tasksRef.current, { type: 'start', id: local.id }) as LocalUpload[]);

    try {
      const body = new FormData();
      body.append('file', { uri: local.uri, name: local.name, type: local.mime } as unknown as Blob);
      const uploaded = await api.uploadReportMedia(
        body,
        (progress) => update(reduceUploads(tasksRef.current, { type: 'progress', id: local.id, progress }) as LocalUpload[]),
        controller.signal,
      );
      update(reduceUploads(tasksRef.current, { type: 'ready', id: local.id, mediaId: uploaded.mediaId }) as LocalUpload[]);
    } catch (error) {
      const cancelled = error instanceof AppError && error.code === 'CANCELLED';
      update(
        reduceUploads(tasksRef.current, cancelled
          ? { type: 'cancel', id: local.id }
          : { type: 'fail', id: local.id, error: error instanceof AppError ? error.userMessage : messageFor('NETWORK') }) as LocalUpload[],
      );
    } finally {
      controllers.current.delete(local.id);
      pumping.current = false;
      void pump();
    }
  }, [update]);

  const enqueue = useCallback((task: LocalUpload) => {
    update(reduceUploads(tasksRef.current, { type: 'enqueue', task }) as LocalUpload[]);
    void pump();
  }, [pump, update]);

  const cancel = useCallback((id: string) => {
    controllers.current.get(id)?.abort();
    update(reduceUploads(tasksRef.current, { type: 'cancel', id }) as LocalUpload[]);
  }, [update]);

  const retry = useCallback((id: string) => {
    update(reduceUploads(tasksRef.current, { type: 'retry', id }) as LocalUpload[]);
    void pump();
  }, [pump, update]);

  return { tasks, enqueue, cancel, retry };
}

export async function prepareImage(uri: string, width: number | undefined): Promise<string> {
  const actions = width && width > reportMediaFallbacks.imageMaxEdge ? [{ resize: { width: reportMediaFallbacks.imageMaxEdge } }] : [];
  const result = await manipulateAsync(uri, actions, { compress: reportMediaFallbacks.imageQuality, format: SaveFormat.JPEG });
  return result.uri;
}
