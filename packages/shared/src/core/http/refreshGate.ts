export function createRefreshGate(refresh: () => Promise<string | null>) {
  let pending: Promise<string | null> | null = null;

  return function refreshOnce(): Promise<string | null> {
    if (!pending) {
      pending = refresh()
        .then((token) => (typeof token === 'string' && token.length > 0 ? token : null))
        .catch(() => null)
        .finally(() => {
          pending = null;
        });
    }

    return pending;
  };
}
