export type PublisherEvent = 'connecting' | 'live' | 'reconnecting' | 'failed';

export type LiveCameraHandle = {
  start: (url: string, key: string) => void;
  stop: () => void;
};

export type LiveCameraProps = {
  onEvent: (event: PublisherEvent) => void;
  onFailure?: (message: string) => void;
};
