import { resolveAppConfig } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { AppState } from 'react-native';

import { api } from '@/lib/api';

import { readAppConfigFile, writeAppConfigFile } from './app-config-storage';

export function useAppConfig() {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ['app-config'],
    queryFn: async () => {
      const cached = await readAppConfigFile();
      try {
        const response = await api.appConfig(cached?.etag);
        const resolved = resolveAppConfig(cached, {
          ok: true,
          status: response.status,
          etag: response.headers.etag ?? null,
          body: response.data ?? null,
        });
        if (resolved.cache && resolved.cache.etag !== cached?.etag) {
          await writeAppConfigFile(resolved.cache);
        }
        return resolved.config;
      } catch {
        return resolveAppConfig(cached, { ok: false }).config;
      }
    },
  });

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        void queryClient.invalidateQueries({ queryKey: ['app-config'] });
      }
    });
    return () => subscription.remove();
  }, [queryClient]);

  return query;
}
