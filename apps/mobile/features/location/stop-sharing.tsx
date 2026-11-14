import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import { api } from '@/lib/api';

/** Permanent location sharing is no longer part of the product. Turn off a session that was left on. */
export function StopLocationSharing() {
  const queryClient = useQueryClient();
  const sharing = useQuery({ queryKey: ['location-sharing'], queryFn: () => api.locationSharing() });
  const stopping = useRef(false);

  useEffect(() => {
    if (!sharing.data?.enabled || stopping.current) {
      return;
    }
    stopping.current = true;
    void api.setLocationSharing({ enabled: false, mode: sharing.data.mode }).then(() => {
      void queryClient.invalidateQueries({ queryKey: ['location-sharing'] });
    }).catch(() => {
      stopping.current = false;
    });
  }, [queryClient, sharing.data]);

  return null;
}
