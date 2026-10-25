import { AppError, notify, shouldRetry } from '@situ/shared';
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';
import type { ReactNode } from 'react';

export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: (failureCount, error) => error instanceof AppError && shouldRetry('GET', error.code, failureCount),
          },
        },
        mutationCache: new MutationCache({
          onError(error, _variables, _context, mutation) {
            if (mutation.meta?.silentError === true) {
              return;
            }

            if (error instanceof AppError && (error.code === 'VALIDATION' || error.code === 'CANCELLED' || error.code === 'UNAUTHORIZED')) {
              return;
            }

            notify.error(error, {
              action: {
                label: 'Reintentar',
                onPress: () => {
                  void mutation.execute(mutation.state.variables);
                },
              },
            });
          },
        }),
      }),
  );

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
