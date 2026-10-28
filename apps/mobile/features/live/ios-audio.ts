import { Platform } from 'react-native';

/** Record while the preview is up. Playback does not continue after the app leaves the foreground. */
export const liveRecordMode = {
  allowsRecording: true,
  playsInSilentMode: true,
  interruptionMode: 'doNotMix',
  shouldPlayInBackground: false,
  shouldRouteThroughEarpiece: false,
  allowsBackgroundRecording: false,
} as const;

/** Hand the session back when the stream stops. */
export const liveIdleMode = {
  allowsRecording: false,
  playsInSilentMode: false,
  interruptionMode: 'mixWithOthers',
  shouldPlayInBackground: false,
  shouldRouteThroughEarpiece: false,
  allowsBackgroundRecording: false,
} as const;

type AudioMode = typeof liveRecordMode | typeof liveIdleMode;

async function apply(mode: AudioMode): Promise<void> {
  if (Platform.OS !== 'ios') {
    return;
  }
  try {
    const audio = require('expo-audio') as {
      setAudioModeAsync: (next: AudioMode) => Promise<void>;
      setIsAudioActiveAsync: (active: boolean) => Promise<void>;
    };
    await audio.setAudioModeAsync(mode);
    await audio.setIsAudioActiveAsync(mode.allowsRecording);
  } catch {
    // HaishinKit still sets playAndRecord when audio is prepared. This restore is extra.
  }
}

export function beginLiveAudio(): Promise<void> {
  return apply(liveRecordMode);
}

export function endLiveAudio(): Promise<void> {
  return apply(liveIdleMode);
}
