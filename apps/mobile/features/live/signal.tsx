import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { useEffect, useState } from 'react';
import { AppState } from 'react-native';

import { apiOrigin, freshAccessToken } from '@/lib/api';

import { readLiveNotice } from './logic';
import { publishLiveNotice, refreshLiveRoster, setHubConnected } from './roster';

/** One map-hub socket. `live` refreshes the roster and tells an open player that its stream ended. */
export function LiveSignal() {
  const [active, setActive] = useState(AppState.currentState === 'active');

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      setActive(state === 'active');
    });
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    if (!active) {
      setHubConnected(false);
      return;
    }

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
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.None)
      .build();
    connection.on('live', (payload: unknown) => {
      const notice = readLiveNotice(payload);
      if (notice) {
        publishLiveNotice(notice);
      }
      refreshLiveRoster();
    });
    connection.onreconnecting(() => setHubConnected(false));
    connection.onreconnected(() => {
      setHubConnected(true);
      refreshLiveRoster();
    });
    connection.onclose(() => setHubConnected(false));
    void connection.start().then(() => setHubConnected(true)).catch(() => setHubConnected(false));
    return () => {
      setHubConnected(false);
      void connection.stop();
    };
  }, [active]);

  return null;
}
