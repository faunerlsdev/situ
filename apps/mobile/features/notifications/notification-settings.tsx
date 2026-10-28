import {
  alertModal,
  AppError,
  confirm,
  notificationMessages,
  notify,
} from '@situ/shared';
import type { NotificationSettings } from '@situ/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as Location from 'expo-location';
import * as Notifications from 'expo-notifications';
import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { Linking, Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';

import { useReportCatalog } from '@/features/reports/use-catalog';
import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

import { forgetAlertLocation } from './alert-location';
import {
  alertCleanupAction,
  formatQuiet,
  nearestRadius,
  notificationFailure,
  quietClock,
  RADIUS_STEPS,
  radiusLabel,
  readPushAvailability,
  refreshPushRegistration,
  subscribePushAvailability,
} from './logic';
import { SettingsToggle } from './settings-toggle';

export function NotificationSettingsScreen() {
  const me = useMe();
  const catalog = useReportCatalog();
  const queryClient = useQueryClient();
  const availability = useSyncExternalStore(subscribePushAvailability, readPushAvailability, readPushAvailability);
  const settingsQuery = useQuery({ queryKey: ['notification-settings'], queryFn: () => api.notificationSettings() });
  const citiesQuery = useQuery({ queryKey: ['interest-cities'], queryFn: () => api.interestCities() });
  const categoriesQuery = useQuery({ queryKey: ['category-interests'], queryFn: () => api.categoryInterests() });
  const [quietStart, setQuietStart] = useState<{ hour: number; minute: number } | null>(null);
  const [quietEnd, setQuietEnd] = useState<{ hour: number; minute: number } | null>(null);
  const [addingCity, setAddingCity] = useState(false);
  const [radius, setRadius] = useState<number | null>(null);
  const radiusTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setQuietStart(quietClock(settingsQuery.data?.quietHoursStart ?? null));
    setQuietEnd(quietClock(settingsQuery.data?.quietHoursEnd ?? null));
  }, [settingsQuery.data?.quietHoursStart, settingsQuery.data?.quietHoursEnd]);

  const saveSettings = useMutation({
    mutationFn: (next: NotificationSettings) => api.updateNotificationSettings(normalizeSettings(next)),
    onMutate: async (next) => {
      await queryClient.cancelQueries({ queryKey: ['notification-settings'] });
      const previous = queryClient.getQueryData<NotificationSettings>(['notification-settings']);
      queryClient.setQueryData(['notification-settings'], normalizeSettings(next));
      return { previous };
    },
    onError: (error, _next, context) => {
      if (context?.previous) {
        queryClient.setQueryData(['notification-settings'], context.previous);
      }
      const kind = notificationFailure(error instanceof AppError ? error.apiCode : undefined);
      if (kind === 'device-required') {
        void alertModal({ title: notificationMessages.deviceRequiredTitle, message: notificationMessages.deviceRequiredBody, type: 'warning' });
        return;
      }
      if (kind === 'minor') {
        void alertModal({ title: notificationMessages.minorTitle, message: notificationMessages.minorBody, type: 'info' });
        return;
      }
      notify.error(error, { placement: 'top' });
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['notification-settings'] });
    },
  });

  const settings = settingsQuery.data;
  if (settingsQuery.isLoading || !settings) {
    return <View style={styles.skeleton} />;
  }

  if (settingsQuery.isError) {
    return (
      <View style={styles.block}>
        <Text style={styles.body}>{notificationMessages.loadError}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.retry} onPress={() => void settingsQuery.refetch()} style={styles.button}>
          <Text style={styles.link}>{notificationMessages.retry}</Text>
        </Pressable>
      </View>
    );
  }

  async function persist(next: NotificationSettings): Promise<boolean> {
    try {
      await saveSettings.mutateAsync(next);
      return true;
    } catch {
      return false;
    }
  }

  const muted = new Set(categoriesQuery.data?.mutedCategoryIds ?? []);
  const categories = [...(catalog.data?.categories ?? [])].sort((a, b) => Number(a.sortOrder) - Number(b.sortOrder));

  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
      {availability === 'unavailable' ? <Text style={styles.note}>{notificationMessages.unavailableDevice}</Text> : null}
      <Text style={styles.note}>
        {availability === 'ready' ? notificationMessages.permissionGranted : availability === 'denied' ? notificationMessages.permissionBlocked : notificationMessages.permissionPending}
      </Text>
      {availability === 'denied' ? (
        <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.openSettings} onPress={() => void Linking.openSettings()} style={styles.button}>
          <Text style={styles.link}>{notificationMessages.openSettings}</Text>
        </Pressable>
      ) : null}
      {availability === 'unknown' ? (
        <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.activate} onPress={() => void enablePushFromSettings()} style={styles.button}>
          <Text style={styles.link}>{notificationMessages.activate}</Text>
        </Pressable>
      ) : null}
      <SettingsToggle
        label={notificationMessages.pushSwitch}
        value={settings.pushEnabled}
        save={(next) => persist({ ...settings, pushEnabled: next })}
      />
      <SettingsToggle
        label={notificationMessages.nearby}
        value={settings.notifyNearbyReports}
        save={async (next) => {
          if (next && me.data?.isMinor) {
            await alertModal({ title: notificationMessages.minorTitle, message: notificationMessages.minorBody, type: 'info' });
          }
          const asked = next ? await Location.requestForegroundPermissionsAsync().catch(() => null) : null;
          if (next && (!asked || asked.status !== 'granted')) {
            notify.warning(notificationMessages.locationDenied, { placement: 'top' });
          }
          const saved = await persist({ ...settings, notifyNearbyReports: next });
          if (saved && alertCleanupAction({
            signingOut: false,
            nearbyEnabled: next,
            locationGranted: !next || asked?.status === 'granted',
            minorBlocked: false,
          }) === 'alert-location') {
            await forgetAlertLocation();
          }
          return saved;
        }}
      />
      <SettingsToggle
        label={notificationMessages.friends}
        value={settings.notifyFriendReports}
        save={(next) => persist({ ...settings, notifyFriendReports: next })}
      />
      <SettingsToggle
        label={notificationMessages.lives}
        value={settings.notifyFriendLives}
        save={(next) => persist({ ...settings, notifyFriendLives: next })}
      />
      <SettingsToggle
        label={notificationMessages.nearbyLives}
        value={settings.notifyNearbyLives}
        save={(next) => persist({ ...settings, notifyNearbyLives: next })}
      />
      <SettingsToggle label={notificationMessages.news} value={false} disabled hint={notificationMessages.comingSoon} save={async () => false} />

      <Text style={styles.heading}>{notificationMessages.proximityTitle}</Text>
      <Text style={styles.body}>{notificationMessages.proximityBody}</Text>
      {me.data?.isMinor ? <Text style={styles.note}>{notificationMessages.minorBody}</Text> : null}
      <View style={styles.steps}>
        {RADIUS_STEPS.map((step) => {
          const selected = (radius ?? nearestRadius(Number(settings.nearbyRadiusMeters))) === step;
          return (
            <Pressable
              key={step}
              accessibilityRole="button"
              accessibilityLabel={radiusLabel(step)}
              accessibilityState={{ selected }}
              onPress={() => {
                setRadius(step);
                if (radiusTimer.current) {
                  clearTimeout(radiusTimer.current);
                }
                radiusTimer.current = setTimeout(() => {
                  void persist({ ...settings, nearbyRadiusMeters: step }).then((ok) => {
                    if (!ok) {
                      setRadius(null);
                    }
                  });
                }, 400);
              }}
              style={[styles.step, selected && styles.stepOn]}
            >
              <Text style={styles.stepLabel}>{radiusLabel(step)}</Text>
            </Pressable>
          );
        })}
      </View>

      <Text style={styles.heading}>{notificationMessages.cities}</Text>
      {(citiesQuery.data?.cities ?? []).map((city) => (
        <View key={city.cityId} style={styles.row}>
          <Text style={styles.body}>{city.name}</Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`${notificationMessages.removeCity} ${city.name}`}
            onPress={() => void removeCity(queryClient, city.cityId, city.name)}
            style={styles.button}
          >
            <Text style={styles.link}>{notificationMessages.removeCity}</Text>
          </Pressable>
        </View>
      ))}
      <Text style={styles.note}>{`${citiesQuery.data?.cities.length ?? 0} / ${Number(citiesQuery.data?.maximum ?? 0)}`}</Text>
      {(citiesQuery.data?.cities.length ?? 0) < Number(citiesQuery.data?.maximum ?? 0) ? (
        <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.addCity} onPress={() => setAddingCity((value) => !value)} style={styles.button}>
          <Text style={styles.link}>{notificationMessages.addCity}</Text>
        </Pressable>
      ) : null}
      {addingCity ? <CityCascade onAdded={() => setAddingCity(false)} /> : null}

      <Text style={styles.heading}>{notificationMessages.categories}</Text>
      <Text style={styles.note}>{notificationMessages.muteHelp}</Text>
      {categories.map((category) => (
        <View key={category.id} style={styles.row}>
          <Text style={styles.body}>{category.name}</Text>
          <Switch
            accessibilityLabel={category.name}
            value={!muted.has(category.id)}
            onValueChange={(on) => void mute(queryClient, muted, category.id, !on)}
            trackColor={{ true: theme.colors.primary, false: theme.colors.border }}
            thumbColor={theme.colors.textOnPrimary}
          />
        </View>
      ))}

      <Text style={styles.heading}>{notificationMessages.quiet}</Text>
      <Text style={styles.note}>{`${notificationMessages.zone}: ${settings.timeZoneId}`}</Text>
      <Clock label={notificationMessages.quietStart} value={quietStart} onChange={setQuietStart} />
      <Clock label={notificationMessages.quietEnd} value={quietEnd} onChange={setQuietEnd} />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={notificationMessages.quiet}
        onPress={() => {
          if (!quietStart || !quietEnd) {
            return;
          }
          void persist({ ...settings, quietHoursStart: formatQuiet(quietStart.hour, quietStart.minute), quietHoursEnd: formatQuiet(quietEnd.hour, quietEnd.minute) });
        }}
        style={styles.button}
      >
        <Text style={styles.link}>{notificationMessages.quietSave}</Text>
      </Pressable>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={notificationMessages.quietOff}
        onPress={() => {
          setQuietStart(null);
          setQuietEnd(null);
          void persist({ ...settings, quietHoursStart: null, quietHoursEnd: null });
        }}
        style={styles.button}
      >
        <Text style={styles.link}>{notificationMessages.quietOff}</Text>
      </Pressable>
    </ScrollView>
  );
}

async function enablePushFromSettings() {
  try {
    const asked = await Notifications.requestPermissionsAsync();
    if (asked.status !== 'granted') {
      await alertModal({
        title: notificationMessages.permissionDeniedTitle,
        message: notificationMessages.permissionDeniedBody,
        type: 'warning',
        buttonText: notificationMessages.openSettings,
      });
      await Linking.openSettings();
    }
  } catch (error) {
    if (__DEV__) {
      console.warn('push permission', error instanceof Error ? error.message : 'unavailable');
    }
  } finally {
    refreshPushRegistration();
  }
}

function normalizeSettings(settings: NotificationSettings): NotificationSettings {
  return {
    ...settings,
    nearbyRadiusMeters: nearestRadius(Number(settings.nearbyRadiusMeters)),
    notifyNearbyLives: settings.notifyNearbyLives !== false,
    quietHoursStart: settings.quietHoursStart,
    quietHoursEnd: settings.quietHoursEnd,
  };
}

async function mute(queryClient: ReturnType<typeof useQueryClient>, muted: Set<string>, id: string, nextMuted: boolean) {
  const previous = [...muted];
  const next = new Set(muted);
  if (nextMuted) {
    next.add(id);
  } else {
    next.delete(id);
  }
  queryClient.setQueryData(['category-interests'], { mutedCategoryIds: [...next] });
  try {
    await api.updateCategoryInterests([...next]);
  } catch (error) {
    queryClient.setQueryData(['category-interests'], { mutedCategoryIds: previous });
    const kind = notificationFailure(error instanceof AppError ? error.apiCode : undefined);
    if (kind === 'device-required') {
      void alertModal({ title: notificationMessages.deviceRequiredTitle, message: notificationMessages.deviceRequiredBody, type: 'warning' });
      return;
    }
    notify.error(error, { placement: 'top' });
  }
}

async function removeCity(queryClient: ReturnType<typeof useQueryClient>, cityId: string, name: string) {
  const accepted = await confirm({
    title: notificationMessages.removeCityTitle,
    message: `${notificationMessages.removeCityBody} ${name}`,
    type: 'warning',
    confirmText: notificationMessages.removeCity,
    cancelText: notificationMessages.notNow,
  });
  if (!accepted) {
    return;
  }

  try {
    const next = await api.removeInterestCity(cityId);
    queryClient.setQueryData(['interest-cities'], next);
  } catch (error) {
    notify.error(error, { placement: 'top' });
  }
}

function Clock({
  label,
  value,
  onChange,
}: {
  label: string;
  value: { hour: number; minute: number } | null;
  onChange: (next: { hour: number; minute: number }) => void;
}) {
  const hour = value?.hour ?? 22;
  const minute = value?.minute ?? 0;
  return (
    <View style={styles.row}>
      <Text style={styles.body}>{label}</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={`${label} menos`} onPress={() => onChange(stepClock(hour, minute, -15))} style={styles.step}>
        <Text style={styles.stepLabel}>−</Text>
      </Pressable>
      <Text style={styles.body}>{formatQuiet(hour, minute)}</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={`${label} más`} onPress={() => onChange(stepClock(hour, minute, 15))} style={styles.step}>
        <Text style={styles.stepLabel}>+</Text>
      </Pressable>
    </View>
  );
}

function stepClock(hour: number, minute: number, delta: number): { hour: number; minute: number } {
  const total = (hour * 60 + minute + delta + 24 * 60) % (24 * 60);
  return { hour: Math.floor(total / 60), minute: total % 60 };
}

function CityCascade({ onAdded }: { onAdded: () => void }) {
  const queryClient = useQueryClient();
  const countries = useQuery({ queryKey: ['countries'], queryFn: () => api.listCountries() });
  const [countryId, setCountryId] = useState<string | null>(null);
  const [regionId, setRegionId] = useState<string | null>(null);
  const regions = useQuery({ queryKey: ['regions', countryId], enabled: countryId !== null, queryFn: () => api.listRegions(countryId ?? '') });
  const cities = useQuery({ queryKey: ['cities', regionId], enabled: regionId !== null, queryFn: () => api.listCities(regionId ?? '') });
  const country = countries.data?.countries.find((item) => item.id === countryId);
  const region = regions.data?.regions.find((item) => item.id === regionId);

  return (
    <View style={styles.block}>
      <Text style={styles.note}>{country?.name ?? 'País'}</Text>
      {(countries.data?.countries ?? []).map((item) => (
        <Pressable key={item.id} accessibilityRole="button" accessibilityLabel={item.name} onPress={() => { setCountryId(item.id); setRegionId(null); }} style={styles.button}>
          <Text style={styles.body}>{item.name}</Text>
        </Pressable>
      ))}
      {countryId ? <Text style={styles.note}>{country?.regionLabel ?? 'Departamento'}</Text> : null}
      {(regions.data?.regions ?? []).map((item) => (
        <Pressable key={item.id} accessibilityRole="button" accessibilityLabel={item.name} onPress={() => setRegionId(item.id)} style={styles.button}>
          <Text style={[styles.body, item.id === region?.id && styles.link]}>{item.name}</Text>
        </Pressable>
      ))}
      {(cities.data?.cities ?? []).map((item) => (
        <Pressable
          key={item.id}
          accessibilityRole="button"
          accessibilityLabel={item.name}
          onPress={() => void addCity(queryClient, item.id, onAdded)}
          style={styles.button}
        >
          <Text style={styles.body}>{item.name}</Text>
        </Pressable>
      ))}
    </View>
  );
}

async function addCity(queryClient: ReturnType<typeof useQueryClient>, cityId: string, onAdded: () => void) {
  try {
    const next = await api.addInterestCity(cityId);
    queryClient.setQueryData(['interest-cities'], next);
    onAdded();
  } catch (error) {
    const kind = notificationFailure(error instanceof AppError ? error.apiCode : undefined);
    if (kind === 'device-required') {
      void alertModal({ title: notificationMessages.deviceRequiredTitle, message: notificationMessages.deviceRequiredBody, type: 'warning' });
      return;
    }
    notify.error(error, { placement: 'top' });
  }
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 14, padding: 20, paddingBottom: 48 },
  skeleton: { flex: 1, backgroundColor: theme.colors.bg },
  block: { gap: 8 },
  heading: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18, marginTop: 8 },
  body: { flex: 1, color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  note: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 20 },
  row: { minHeight: 44, flexDirection: 'row', alignItems: 'center', gap: 12 },
  button: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  steps: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  step: { minHeight: 44, minWidth: 44, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 12, borderRadius: theme.radius.button, borderWidth: 1, borderColor: theme.colors.border },
  stepOn: { borderColor: theme.colors.primary, backgroundColor: theme.colors.primarySoft },
  stepLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 15 },
});
