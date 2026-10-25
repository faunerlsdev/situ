import { resolveCatalog } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { AppState } from 'react-native';

import { api } from '@/lib/api';

import { readCatalogFile, writeCatalogFile } from './catalog-storage';

export function useReportCatalog() {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ['report-catalog'],
    queryFn: async () => {
      const cached = await readCatalogFile();
      try {
        const response = await api.reportCategories(cached?.etag);
        const resolved = resolveCatalog(cached, {
          status: response.status,
          etag: response.headers.etag ?? null,
          body: response.data ?? null,
        });
        if (resolved.cache && !resolved.unchanged) {
          await writeCatalogFile(resolved.cache);
        }
        if (!resolved.catalog) {
          throw new Error('catalog-empty');
        }
        return resolved.catalog;
      } catch (error) {
        if (cached) {
          return cached.body;
        }
        throw error;
      }
    },
  });

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        void queryClient.invalidateQueries({ queryKey: ['report-catalog'] });
      }
    });
    return () => subscription.remove();
  }, [queryClient]);

  return query;
}
