import { ApiError } from '@situ/shared';
import type { AuthSession } from '@situ/shared';
import * as LocalAuthentication from 'expo-local-authentication';
import * as SecureStore from 'expo-secure-store';
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

import { api, setAccessToken, setSessionHandlers } from '@/lib/api';

const accessKey = 'situ.accessToken';
const refreshKey = 'situ.refreshToken';
const expiresKey = 'situ.accessExpiresAt';
const biometricKey = 'situ.biometricUnlock';

type SessionContextValue = {
  isReady: boolean;
  isSignedIn: boolean;
  biometricAvailable: boolean;
  canUnlockWithBiometric: boolean;
  signIn: (session: AuthSession, biometric: boolean) => Promise<void>;
  signOut: () => Promise<void>;
  unlockWithBiometric: () => Promise<boolean>;
};

const SessionContext = createContext<SessionContextValue | null>(null);

let refreshInFlight: Promise<string | null> | null = null;
let biometricLocked = false;

async function persist(session: AuthSession) {
  const expiresAt = session.accessTokenExpiresAt ?? new Date(Date.now() + 15 * 60 * 1000).toISOString();
  await SecureStore.setItemAsync(accessKey, session.accessToken);
  await SecureStore.setItemAsync(refreshKey, session.refreshToken);
  await SecureStore.setItemAsync(expiresKey, expiresAt);
  setAccessToken(session.accessToken);
}

async function clearStoredSession() {
  await SecureStore.deleteItemAsync(accessKey);
  await SecureStore.deleteItemAsync(refreshKey);
  await SecureStore.deleteItemAsync(expiresKey);
  setAccessToken(null);
}

async function refreshOnce(): Promise<string | null> {
  if (!refreshInFlight) {
    refreshInFlight = doRefresh().finally(() => {
      refreshInFlight = null;
    });
  }

  return refreshInFlight;
}

async function doRefresh(): Promise<string | null> {
  const refreshToken = await SecureStore.getItemAsync(refreshKey);
  if (!refreshToken) {
    return null;
  }

  try {
    const session = await api.refresh(refreshToken);
    if (!session) {
      await clearStoredSession();
      return null;
    }

    await persist(session);
    return session.accessToken;
  } catch (cause) {
    if (cause instanceof ApiError && (cause.status === 401 || cause.status === 403)) {
      await clearStoredSession();
    }

    return null;
  }
}

async function ensureFresh() {
  if (biometricLocked) {
    return;
  }
  const accessToken = await SecureStore.getItemAsync(accessKey);
  const expiresAt = await SecureStore.getItemAsync(expiresKey);
  if (!accessToken) {
    setAccessToken(null);
    return;
  }

  const expires = expiresAt ? Date.parse(expiresAt) : Number.NaN;
  if (!Number.isNaN(expires) && expires - Date.now() > 30_000) {
    setAccessToken(accessToken);
    return;
  }

  await refreshOnce();
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [isReady, setIsReady] = useState(false);
  const [isSignedIn, setSignedIn] = useState(false);
  const [biometricAvailable, setBiometricAvailable] = useState(false);
  const [canUnlockWithBiometric, setCanUnlock] = useState(false);

  const signOut = useCallback(async () => {
    biometricLocked = false;
    const refreshToken = await SecureStore.getItemAsync(refreshKey);
    if (refreshToken) {
      await api.signOut(refreshToken).catch(() => undefined);
    }

    await clearStoredSession();
    await SecureStore.deleteItemAsync(biometricKey);
    setCanUnlock(false);
    setSignedIn(false);
  }, []);

  const applyRefreshFailure = useCallback(async () => {
    await clearStoredSession();
    setCanUnlock(false);
    setSignedIn(false);
  }, []);

  setSessionHandlers({
    prepareRequest: ensureFresh,
    refreshAccessToken: async () => {
      const next = await refreshOnce();
      if (!next) {
        const remaining = await SecureStore.getItemAsync(refreshKey);
        if (!remaining) {
          await applyRefreshFailure();
        }
      }

      return next;
    },
  });

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [hardware, enrolled, accessToken, refreshToken, biometric] = await Promise.all([
          LocalAuthentication.hasHardwareAsync(),
          LocalAuthentication.isEnrolledAsync(),
          SecureStore.getItemAsync(accessKey),
          SecureStore.getItemAsync(refreshKey),
          SecureStore.getItemAsync(biometricKey),
        ]);
        if (cancelled) {
          return;
        }

        setBiometricAvailable(hardware && enrolled);
        if (accessToken && refreshToken && biometric === '1') {
          biometricLocked = true;
          setAccessToken(null);
          setCanUnlock(true);
          setSignedIn(false);
          return;
        }

        biometricLocked = false;

        if (accessToken && refreshToken) {
          setAccessToken(accessToken);
          setSignedIn(true);
          return;
        }

        setAccessToken(null);
        setSignedIn(false);
      } catch {
        if (!cancelled) {
          setAccessToken(null);
          setSignedIn(false);
        }
      } finally {
        if (!cancelled) {
          setIsReady(true);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  const signIn = useCallback(async (session: AuthSession, biometric: boolean) => {
    biometricLocked = false;
    await persist(session);
    if (biometric) {
      await SecureStore.setItemAsync(biometricKey, '1');
    } else {
      await SecureStore.deleteItemAsync(biometricKey);
    }

    setCanUnlock(false);
    setSignedIn(true);
  }, []);

  const unlockWithBiometric = useCallback(async () => {
    const result = await LocalAuthentication.authenticateAsync({
      promptMessage: 'Desbloquea SITU',
      cancelLabel: 'Usar contraseña',
      disableDeviceFallback: true,
    });
    if (!result.success) {
      return false;
    }

    biometricLocked = false;

    const accessToken = await SecureStore.getItemAsync(accessKey);
    if (!accessToken) {
      return false;
    }

    setAccessToken(accessToken);
    setCanUnlock(false);
    setSignedIn(true);
    void ensureFresh();
    return true;
  }, []);

  const value = useMemo(
    () => ({
      isReady,
      isSignedIn,
      biometricAvailable,
      canUnlockWithBiometric,
      signIn,
      signOut,
      unlockWithBiometric,
    }),
    [biometricAvailable, canUnlockWithBiometric, isReady, isSignedIn, signIn, signOut, unlockWithBiometric],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (!value) {
    throw new Error('useSession must be used within SessionProvider');
  }

  return value;
}
