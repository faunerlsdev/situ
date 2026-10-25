import { alertModal, AppError, notificationMessages, notify } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Location from 'expo-location';
import * as Notifications from 'expo-notifications';
import { useRouter } from 'expo-router';
import * as SecureStore from 'expo-secure-store';
import { Component, useEffect, useState } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import { AppState, Modal, Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import { api, isDeviceLimitSession, isGuardianSession } from '@/lib/api';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

import { forgetAlertLocation, useAlertLocationSync } from './alert-location';
import {
  alertCleanupAction,
  alertLocationPaused,
  alertSyncEnabled,
  ANDROID_ALERT_CHANNEL_ID,
  isMinorProximityBlocked,
  notificationFailure,
  pushDataTarget,
  pushRegistrationPlan,
  registerPushRefresh,
  reportOpenFailure,
  setPushAvailability,
  shouldRetryPush,
} from './logic';

const explainerKey = 'situ.push.explainer';
const tokenKey = 'situ.push.token';
const brand = '#FF6400';

type Plan = ReturnType<typeof pushRegistrationPlan>;

export function NotificationRuntime() {
  return (
    <PushBoundary>
      <PushRuntime />
    </PushBoundary>
  );
}

function PushRuntime() {
  const { isSignedIn } = useSession();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [plan, setPlan] = useState<Plan | null>(null);
  const [appActive, setAppActive] = useState(AppState.currentState === 'active');
  const [locationGranted, setLocationGranted] = useState(false);
  const settings = useQuery({
    queryKey: ['notification-settings'],
    enabled: isSignedIn,
    queryFn: () => api.notificationSettings(),
  });
  const unread = useQuery({
    queryKey: ['notifications', 'unread'],
    enabled: isSignedIn,
    queryFn: () => api.unreadNotificationCount(),
  });

  useEffect(() => {
    if (!isSignedIn) {
      setPlan(null);
      return;
    }

    let cancelled = false;
    void (async () => {
      const next = await inspectPush();
      if (!cancelled) {
        setPlan(next.plan);
        setPushAvailability(availabilityFor(next.plan));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isSignedIn]);

  useEffect(() => {
    if (plan !== 'register') {
      return;
    }

    let cancelled = false;
    void (async () => {
      const token = await readExpoToken();
      if (cancelled || !token) {
        return;
      }
      await publishToken(token);
    })();
    return () => {
      cancelled = true;
    };
  }, [plan, isSignedIn]);

  useEffect(() => {
    if (plan !== 'register') {
      return;
    }

    const subscription = AppState.addEventListener('change', (state) => {
      if (state !== 'active') {
        return;
      }

      void readExpoToken().then(async (token) => {
        if (!token) {
          return;
        }
        const stored = await SecureStore.getItemAsync(tokenKey).catch(() => null);
        if (stored !== token) {
          await publishToken(token);
        }
      });
    });
    return () => subscription.remove();
  }, [plan]);

  useEffect(() => {
    if (!isSignedIn) {
      return;
    }

    try {
      Notifications.setNotificationHandler({
        handleNotification: async () => ({
          shouldShowBanner: false,
          shouldShowList: true,
          shouldPlaySound: false,
          shouldSetBadge: false,
        }),
      });
    } catch (error) {
      if (__DEV__) {
        console.warn('push handler', error instanceof Error ? error.message : 'unavailable');
      }
    }

    const received = Notifications.addNotificationReceivedListener((notification) => {
      const title = notification.request.content.title;
      const body = notification.request.content.body;
      const text = [title, body].filter((part) => typeof part === 'string' && part.length > 0).join('. ');
      notify.info(text || notificationMessages.foregroundBanner, { placement: 'top' });
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'unread'] });
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'list'] });
    });
    const opened = Notifications.addNotificationResponseReceivedListener((response) => {
      openPush(router, queryClient, response.notification.request.identifier, response.notification.request.content.data);
    });
    void Notifications.getLastNotificationResponseAsync()
      .then((response) => {
        if (response) {
          openPush(router, queryClient, response.notification.request.identifier, response.notification.request.content.data);
        }
      })
      .catch(() => undefined);

    return () => {
      received.remove();
      opened.remove();
    };
  }, [isSignedIn, queryClient, router]);

  useEffect(() => {
    const count = unread.data?.count ?? 0;
    void Notifications.setBadgeCountAsync(count).catch(() => undefined);
  }, [unread.data?.count]);

  useEffect(() => {
    if (!isSignedIn) {
      return;
    }

    return registerPushRefresh(() => {
      void inspectPush().then((next) => {
        setPlan(next.plan);
        setPushAvailability(availabilityFor(next.plan));
      });
    });
  }, [isSignedIn]);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active' && isSignedIn) {
        void queryClient.invalidateQueries({ queryKey: ['notifications', 'unread'] });
        void inspectPush().then((next) => {
          setPlan(next.plan);
          setPushAvailability(availabilityFor(next.plan));
        });
      }
    });
    return () => subscription.remove();
  }, [isSignedIn, queryClient]);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => setAppActive(state === 'active'));
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    if (!isSignedIn || settings.data?.notifyNearbyReports !== true) {
      setLocationGranted(false);
      return;
    }

    void Location.getForegroundPermissionsAsync()
      .then((result) => {
        const granted = result.status === 'granted';
        setLocationGranted(granted);
        if (alertCleanupAction({ signingOut: false, nearbyEnabled: true, locationGranted: granted, minorBlocked: isMinorProximityBlocked() }) === 'alert-location') {
          void forgetAlertLocation();
        }
      })
      .catch(() => setLocationGranted(false));
  }, [appActive, isSignedIn, settings.data?.notifyNearbyReports]);

  useAlertLocationSync(alertSyncEnabled({
    pushEnabled: settings.data?.pushEnabled === true,
    notifyNearbyReports: settings.data?.notifyNearbyReports === true,
    locationGranted,
    appActive,
    sessionHasDevice: isSignedIn && !isDeviceLimitSession() && !isGuardianSession(),
    minorBlocked: isMinorProximityBlocked(),
  }) && !alertLocationPaused());

  return (
    <Modal visible={plan === 'ask'} transparent animationType="fade" onRequestClose={() => void dismissExplainer(setPlan)}>
      <View style={styles.backdrop}>
        <View style={styles.card}>
          <Text style={styles.title}>{notificationMessages.explainerTitle}</Text>
          <Text style={styles.body}>{notificationMessages.explainerBody}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.activate} onPress={() => void activate(setPlan)} style={styles.primary}>
            <Text style={styles.primaryLabel}>{notificationMessages.activate}</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.notNow} onPress={() => void dismissExplainer(setPlan)} style={styles.secondary}>
            <Text style={styles.secondaryLabel}>{notificationMessages.notNow}</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

let openedNotificationId: string | null = null;

function openPush(
  router: ReturnType<typeof useRouter>,
  queryClient: ReturnType<typeof useQueryClient>,
  identifier: string,
  data: unknown,
) {
  if (openedNotificationId === identifier) {
    return;
  }

  openedNotificationId = identifier;
  const record = data && typeof data === 'object' ? data as Record<string, unknown> : null;
  void followPush(router, queryClient, pushDataTarget(record));
}

async function followPush(
  router: ReturnType<typeof useRouter>,
  queryClient: ReturnType<typeof useQueryClient>,
  target: ReturnType<typeof pushDataTarget>,
) {
  if (target.kind !== 'unavailable' && target.notificationId) {
    try {
      await api.markNotificationRead(target.notificationId);
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'unread'] });
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'list'] });
    } catch {
      // The inbox can mark it again if this request fails.
    }
  }

  if (target.kind !== 'report') {
    router.navigate('/notificaciones');
    return;
  }

  try {
    await api.report(target.reportId);
  } catch (error) {
    if (reportOpenFailure(error instanceof AppError ? error.apiCode : undefined) === 'unavailable') {
      notify.warning(notificationMessages.unavailableNotice, { placement: 'top' });
      router.navigate('/notificaciones');
      return;
    }
  }

  router.push({ pathname: '/reporte/[id]', params: { id: target.reportId, focus: target.focusComments ? 'comentarios' : '' } });
}

async function inspectPush(): Promise<{ plan: Plan; permission: 'undetermined' | 'granted' | 'denied' }> {
  try {
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync(ANDROID_ALERT_CHANNEL_ID, {
        name: 'Alertas de tu zona',
        importance: Notifications.AndroidImportance.HIGH,
        lightColor: brand,
      });
    }

    const current = await Notifications.getPermissionsAsync();
    const permission = current.status === 'granted' ? 'granted' : current.status === 'denied' ? 'denied' : 'undetermined';
    const dismissed = await SecureStore.getItemAsync(explainerKey) === 'later';
    const plan = pushRegistrationPlan({
      physicalDevice: Device.isDevice,
      permission,
      projectId: readProjectId(),
      dismissed,
    });
    if (plan === 'missing-project' && __DEV__) {
      console.warn('Push needs extra.eas.projectId. The rest of the app keeps working.');
    }
    if (plan === 'simulator' && __DEV__) {
      console.warn('Push stays off on a simulator.');
    }
    return { plan, permission };
  } catch (error) {
    if (__DEV__) {
      console.warn('push inspect', error instanceof Error ? error.message : 'unavailable');
    }
    return { plan: 'simulator', permission: 'undetermined' };
  }
}

async function activate(setPlan: (plan: Plan) => void) {
  try {
    const asked = await Notifications.requestPermissionsAsync();
    const permission = asked.status === 'granted' ? 'granted' : 'denied';
    const plan = pushRegistrationPlan({
      physicalDevice: Device.isDevice,
      permission,
      projectId: readProjectId(),
      dismissed: false,
    });
    setPlan(plan);
    setPushAvailability(availabilityFor(plan));
    if (permission === 'denied') {
      await alertModal({
        title: notificationMessages.permissionDeniedTitle,
        message: notificationMessages.permissionDeniedBody,
        type: 'warning',
        buttonText: notificationMessages.openSettings,
      });
      const { Linking } = await import('react-native');
      await Linking.openSettings();
    }
  } catch (error) {
    if (__DEV__) {
      console.warn('push permission', error instanceof Error ? error.message : 'unavailable');
    }
    setPlan('simulator');
    setPushAvailability('unavailable');
  }
}

async function dismissExplainer(setPlan: (plan: Plan) => void) {
  await SecureStore.setItemAsync(explainerKey, 'later').catch(() => undefined);
  setPlan('later');
  setPushAvailability('unknown');
}

async function readExpoToken(): Promise<string | null> {
  const projectId = readProjectId();
  if (!projectId) {
    return null;
  }

  try {
    const token = await Notifications.getExpoPushTokenAsync({ projectId });
    return token.data;
  } catch (error) {
    if (__DEV__) {
      console.warn('push token', error instanceof Error ? error.message : 'unavailable');
    }
    setPushAvailability('unavailable');
    return null;
  }
}

async function publishToken(token: string) {
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    try {
      await api.setPushToken(token);
      await SecureStore.setItemAsync(tokenKey, token);
      setPushAvailability('ready');
      return;
    } catch (error) {
      const apiCode = error instanceof AppError ? error.apiCode : undefined;
      if (notificationFailure(apiCode) === 'device-required') {
        void alertModal({
          title: notificationMessages.deviceRequiredTitle,
          message: notificationMessages.deviceRequiredBody,
          type: 'warning',
        });
        return;
      }

      const code = error instanceof AppError ? error.code : 'unknown';
      if (!shouldRetryPush(code, attempt)) {
        if (__DEV__) {
          console.warn('push register', code);
        }
        return;
      }

      await new Promise((resolve) => setTimeout(resolve, attempt * 1000));
    }
  }
}

function readProjectId(): string | null {
  const extra = Constants.expoConfig?.extra;
  if (!extra || typeof extra !== 'object' || !('eas' in extra)) {
    return null;
  }

  const eas = extra.eas;
  if (!eas || typeof eas !== 'object' || !('projectId' in eas)) {
    return null;
  }

  const projectId = eas.projectId;
  return typeof projectId === 'string' && projectId.length > 0 ? projectId : null;
}

function availabilityFor(plan: Plan): 'ready' | 'denied' | 'unavailable' | 'unknown' {
  if (plan === 'register') {
    return 'ready';
  }
  if (plan === 'denied') {
    return 'denied';
  }
  if (plan === 'ask' || plan === 'later') {
    return 'unknown';
  }
  return 'unavailable';
}

class PushBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError(): { failed: boolean } {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    if (__DEV__) {
      console.warn('push runtime stopped', error.message, info.componentStack);
    }
  }

  render() {
    return this.state.failed ? null : this.props.children;
  }
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'center', padding: 24, backgroundColor: theme.colors.overlay },
  card: { gap: 12, borderRadius: theme.radius.card, padding: 20, backgroundColor: theme.colors.surfaceElevated },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 22, lineHeight: 28 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  primary: { minHeight: 44, alignItems: 'center', justifyContent: 'center', borderRadius: theme.radius.button, backgroundColor: theme.colors.primary },
  primaryLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  secondary: { minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  secondaryLabel: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
