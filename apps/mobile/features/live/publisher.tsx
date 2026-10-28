import { forwardRef, useImperativeHandle } from 'react';

import type { LiveCameraHandle, LiveCameraProps } from '@/features/live/publisher-types';

/** Double used on web, in tests, and anywhere the native publisher is not linked. */
export function livePublisherAvailable(): boolean {
  return false;
}

export function requestLiveCapture(): Promise<boolean> {
  return Promise.resolve(false);
}

export const LiveCamera = forwardRef<LiveCameraHandle, LiveCameraProps>(function LiveCamera(_props, ref) {
  useImperativeHandle(ref, () => ({
    start() {
      return undefined;
    },
    stop() {
      return undefined;
    },
  }));
  return null;
});
