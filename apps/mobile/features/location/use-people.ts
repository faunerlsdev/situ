import type { MapPerson } from '@situ/shared';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { AppState } from 'react-native';
import type { Region } from 'react-native-maps';

import { api } from '@/lib/api';

import { createFriendsConnection } from './connection';
import { clusterPeople, mergePeople, type PersonMarker } from './logic';

export function usePeople(options: { active: boolean; region: Region | null; selfId: string | null }): PersonMarker[] {
  const [appState, setAppState] = useState(AppState.currentState);
  const [friends, setFriends] = useState<MapPerson[]>([]);
  const spanOk = options.region !== null && options.region.latitudeDelta <= 3 && options.region.longitudeDelta <= 3;
  const publics = useQuery({
    queryKey: ['map-people', options.region?.latitude, options.region?.longitude, options.region?.latitudeDelta, options.region?.longitudeDelta],
    enabled: options.active && spanOk,
    queryFn: async () => {
      const region = options.region;
      if (!region) {
        return [];
      }
      try {
        const response = await api.mapPeople({
          minLat: region.latitude - region.latitudeDelta / 2,
          maxLat: region.latitude + region.latitudeDelta / 2,
          minLon: region.longitude - region.longitudeDelta / 2,
          maxLon: region.longitude + region.longitudeDelta / 2,
        });
        return response.people;
      } catch {
        return [];
      }
    },
  });

  useEffect(() => {
    const subscription = AppState.addEventListener('change', setAppState);
    return () => subscription.remove();
  }, []);

  const hold = options.active && appState === 'active';

  useEffect(() => {
    if (!hold) {
      return;
    }
    let stopped = false;
    const connection = createFriendsConnection({
      onSnapshot: (people) => {
        if (!stopped) {
          setFriends(people);
        }
      },
      onUpdate: (person) => {
        if (stopped) {
          return;
        }
        setFriends((current) => [...current.filter((item) => item.userId !== person.userId), person]);
      },
      onRemove: (userId) => {
        if (!stopped) {
          setFriends((current) => current.filter((item) => item.userId !== userId));
        }
      },
    });
    void connection.start().catch(() => undefined);
    return () => {
      stopped = true;
      void connection.stop();
    };
  }, [hold]);

  return useMemo(
    () => clusterPeople(mergePeople(friends, publics.data ?? [], options.selfId), options.region?.latitudeDelta ?? 0.05),
    [friends, options.region?.latitudeDelta, options.selfId, publics.data],
  );
}
