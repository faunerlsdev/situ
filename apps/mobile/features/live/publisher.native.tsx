import { forwardRef, useEffect, useImperativeHandle, useMemo, useRef } from 'react';
import { Platform } from 'react-native';
import type { RtmpPublisherViewMethods } from 'react-native-nitro-rtmp-publisher';

import { livePocAudio, livePocVideo } from '@/features/live-poc/urls';
import { liveDebug } from '@/features/live/debug';
import { beginLiveAudio, endLiveAudio } from '@/features/live/ios-audio';
import { nativePublishUrl, publishUrlError, publisherFailureCopy, rtmpTargetShape } from '@/features/live/logic';
import type { LiveCameraHandle, LiveCameraProps, PublisherEvent } from '@/features/live/publisher-types';

type PublisherModule = typeof import('react-native-nitro-rtmp-publisher');

function loadPublisher(): PublisherModule | null {
  try {
    return require('react-native-nitro-rtmp-publisher') as PublisherModule;
  } catch {
    return null;
  }
}

const loaded = loadPublisher();

export function livePublisherAvailable(): boolean {
  return loaded !== null;
}

export async function requestLiveCapture(): Promise<boolean> {
  if (!loaded) {
    return false;
  }
  try {
    const result = await loaded.requestRtmpPermissions();
    return result.granted === true;
  } catch {
    return false;
  }
}

export const LiveCamera = forwardRef<LiveCameraHandle, LiveCameraProps>(function LiveCamera({ onEvent, onFailure }, ref) {
  const camera = useRef<RtmpPublisherViewMethods | null>(null);
  const queued = useRef<{ url: string; key: string } | null>(null);
  const onEventRef = useRef(onEvent);
  const onFailureRef = useRef(onFailure);
  onEventRef.current = onEvent;
  onFailureRef.current = onFailure;

  function publish(target: { url: string; key: string }) {
    const current = camera.current;
    const targetError = target.url ? publishUrlError(target.url) : 'La dirección de publicación no es válida.';
    if (!current || !target.url || !target.key || targetError) {
      liveDebug('publish-blocked', { ready: current !== null, keyPresent: target.key.length > 0, reason: targetError });
      onFailureRef.current?.(targetError ?? 'No se pudo abrir la cámara.');
      return;
    }
    const platform = Platform.OS === 'android' ? 'android' : 'ios';
    const streamUrl = nativePublishUrl(target.url, platform);
    liveDebug('publish', { ...rtmpTargetShape(target.url), keyPresent: true, platform });
    current.startStream(streamUrl);
    liveDebug('publish-called', { tls: true, platform });
    onEventRef.current('connecting');
  }

  useEffect(() => () => {
    void endLiveAudio();
  }, []);

  useImperativeHandle(ref, () => ({
    start(url: string, key: string) {
      if (!camera.current) {
        queued.current = { url, key };
        return;
      }
      publish({ url, key });
    },
    stop() {
      queued.current = null;
      camera.current?.stopStream();
      void endLiveAudio();
    },
  }));

  const hybridRef = useMemo(() => {
    if (!loaded) {
      return undefined;
    }
    const nitro = require('react-native-nitro-modules') as {
      callback: (fn: (view: RtmpPublisherViewMethods) => void) => (view: RtmpPublisherViewMethods) => void;
    };
    return nitro.callback((view) => {
      camera.current = view;
      const rotation = view.getCameraOrientation();
      liveDebug('publisher-step', { step: 'prepare-video' });
      view.prepareVideo(livePocVideo.width, livePocVideo.height, livePocVideo.fps, livePocVideo.bitrate, livePocVideo.iFrameInterval, rotation);
      void (async () => {
        liveDebug('publisher-step', { step: 'audio-session' });
        await beginLiveAudio();
        liveDebug('publisher-step', { step: 'prepare-audio' });
        view.prepareAudio(livePocAudio.bitrate, livePocAudio.sampleRate, livePocAudio.stereo);
        liveDebug('publisher-step', { step: 'start-preview' });
        view.startPreview('back', livePocVideo.width, livePocVideo.height);
        view.setAutoReconnect(0, 0);
        view.setOnConnectionEvent((event, message) => {
          const name = connectionEventName(event);
          liveDebug('publisher', { event: name, detail: typeof message === 'string' ? message : '' });
          if (name === 'connectionFailed' || name === 'authError') {
            onFailureRef.current?.(publisherFailureCopy(name, typeof message === 'string' ? message : ''));
            return;
          }
          onEventRef.current(mapConnection(name));
        });
        view.setOnBitrateChange((bitrate) => {
          liveDebug('publisher-bitrate', { bitrate });
        });
        view.setOnStreamStats((bitrateBps, videoFps) => {
          liveDebug('publisher-stats', { bitrateBps, videoFps });
        });
        view.setOnAudioDriftCorrection((correctionMs, totalCorrectionMs) => {
          liveDebug('publisher-audio-drift', { correctionMs, totalCorrectionMs });
        });
        view.setOnThermalWarning((status) => {
          liveDebug('publisher-thermal', { status: connectionEventName(status) || String(status) });
        });
        liveDebug('publisher-step', { step: 'listeners-ready' });
        const pending = queued.current;
        if (pending) {
          queued.current = null;
          publish(pending);
        }
      })();
    });
  }, []);

  if (!loaded) {
    return null;
  }

  const Preview = loaded.RtmpPublisherView;
  return (
    <Preview
      style={{ flex: 1 }}
      forceHardwareCodec
      videoCodec="h264"
      audioCodec="aac"
      aspectRatioMode="adjust"
      mirrorPreview={false}
      mirrorStream={false}
      thermalWarningThreshold="severe"
      audioSource="camcorder"
      noiseSuppression={false}
      autoRotateStream
      streamMode="lowLatency"
      foregroundServiceTitle=""
      foregroundServiceText=""
      foregroundServiceIcon=""
      pictureInPictureEnabled={false}
      hybridRef={hybridRef as never}
    />
  );
});

function connectionEventName(event: unknown): string {
  if (typeof event === 'string') {
    return event;
  }
  if (event && typeof event === 'object' && 'value' in event && typeof event.value === 'string') {
    return event.value;
  }
  return '';
}

function mapConnection(event: string): PublisherEvent {
  if (event === 'connectionSuccess' || event === 'authSuccess') {
    return 'live';
  }
  if (event === 'reconnecting') {
    return 'reconnecting';
  }
  if (event === 'connectionFailed' || event === 'disconnect' || event === 'authError') {
    return 'failed';
  }
  return 'connecting';
}
