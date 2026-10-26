import { HubConnection, HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';

import { apiOrigin, freshAccessToken } from '@/lib/api';

import { nextRetryDelayMs, type ZoneEvent } from './logic';

export function createMapConnection(onZone: (events: ZoneEvent[]) => void): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(`${apiOrigin()}/hubs/map`, {
      transport: HttpTransportType.WebSockets,
      accessTokenFactory: async () => {
        const token = await freshAccessToken();
        if (!token) {
          throw new Error('session');
        }
        return token;
      },
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retry) => nextRetryDelayMs(retry.previousRetryCount),
    })
    .configureLogging(LogLevel.None)
    .build();

  connection.on('zone', (events: ZoneEvent[] | ZoneEvent) => {
    onZone(Array.isArray(events) ? events : events ? [events] : []);
  });
  return connection;
}
