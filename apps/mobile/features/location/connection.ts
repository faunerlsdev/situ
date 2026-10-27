import { HubConnection, HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';

import { apiOrigin, freshAccessToken } from '@/lib/api';
import type { MapPerson } from '@situ/shared';

import { nextRetryDelayMs } from '@/features/reports/realtime/logic';

export function createFriendsConnection(handlers: {
  onSnapshot: (people: MapPerson[]) => void;
  onUpdate: (person: MapPerson) => void;
  onRemove: (userId: string) => void;
}): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(`${apiOrigin()}/hubs/friends`, {
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

  connection.on('locations', (people: MapPerson[] | null) => {
    handlers.onSnapshot(Array.isArray(people) ? people : []);
  });
  connection.on('location', (person: MapPerson | null) => {
    if (person?.userId) {
      handlers.onUpdate(person);
    }
  });
  connection.on('locationRemoved', (signal: { userId?: string } | null) => {
    if (signal?.userId) {
      handlers.onRemove(signal.userId);
    }
  });
  return connection;
}
