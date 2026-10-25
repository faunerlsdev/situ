import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { AppState } from 'react-native';

import { api } from '@/lib/api';
import { useSession } from '@/providers/session-provider';

export function useMe() {
  const { isSignedIn } = useSession();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ['me'],
    enabled: isSignedIn,
    queryFn: () => api.sessionProfile(),
  });

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        void queryClient.invalidateQueries({ queryKey: ['me'] });
      }
    });
    return () => subscription.remove();
  }, [queryClient]);

  return query;
}
