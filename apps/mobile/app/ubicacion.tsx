import { notify } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import MapView, { Marker, PROVIDER_DEFAULT } from 'react-native-maps';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const bogota = { latitude: 4.711, longitude: -74.072, latitudeDelta: 0.05, longitudeDelta: 0.05 };

export default function LocationSharingScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const sharing = useQuery({ queryKey: ['location-sharing'], queryFn: () => api.locationSharing() });
  const friends = useQuery({ queryKey: ['friends'], queryFn: () => api.friends() });
  const zones = useQuery({ queryKey: ['privacy-zones'], queryFn: () => api.privacyZones() });
  const data = sharing.data;
  const [accepted, setAccepted] = useState(false);
  const [publicAccepted, setPublicAccepted] = useState(false);
  const [pending, setPending] = useState(false);
  const [picked, setPicked] = useState<string[] | null>(null);
  const [zoneName, setZoneName] = useState('Casa');
  const [zoneRadius, setZoneRadius] = useState(300);
  const [zoneMode, setZoneMode] = useState<'Hidden' | 'Approximate'>('Hidden');
  const [center, setCenter] = useState({ latitude: bogota.latitude, longitude: bogota.longitude });
  const selected = picked ?? data?.friendIds ?? [];
  const locked = data?.isMinor === true && data.guardianAllows !== true;

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['location-sharing'] });
    await queryClient.invalidateQueries({ queryKey: ['privacy-zones'] });
  }

  async function save(next: { enabled: boolean; mode?: string; duration?: string; ghost?: string; friendIds?: string[] }) {
    if (!data) {
      return;
    }
    if (next.enabled && locked) {
      notify.warning('Tu representante todavía no autoriza compartir ubicación.');
      return;
    }
    if (next.enabled && !data.consentGranted && !accepted) {
      notify.warning('Lee el texto y marca que estás de acuerdo.');
      return;
    }
    setPending(true);
    try {
      await api.setLocationSharing({
        enabled: next.enabled,
        mode: next.mode ?? data.mode,
        duration: next.duration,
        friendIds: next.friendIds,
        consentVersion: next.enabled ? data.requiredConsentVersion : null,
        ghost: next.ghost,
      });
      await refresh();
      notify.success(next.enabled ? 'Estás compartiendo tu ubicación.' : 'Dejaste de compartir tu ubicación.');
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  async function savePublic(mode: string) {
    if (!data || data.isMinor) {
      notify.warning('Un menor de edad no aparece en el mapa general.');
      return;
    }
    if (mode !== 'Hidden' && !data.profilePublic) {
      notify.warning('Primero haz público tu perfil.');
      return;
    }
    if (mode !== 'Hidden' && !data.publicConsentGranted && !publicAccepted) {
      notify.warning('Lee el texto y marca que estás de acuerdo.');
      return;
    }
    setPending(true);
    try {
      await api.setPublicLocation({ mode, consentVersion: mode === 'Hidden' ? null : data.publicConsentVersion });
      await refresh();
      notify.success(mode === 'Hidden' ? 'Tu ubicación salió del mapa general.' : 'Tu ubicación del mapa general quedó guardada.');
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  async function addZone() {
    setPending(true);
    try {
      await api.savePrivacyZone({
        name: zoneName,
        latitude: center.latitude,
        longitude: center.longitude,
        radiusMeters: zoneRadius,
        mode: zoneMode,
      });
      await refresh();
      notify.success('Zona guardada.');
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 12, paddingBottom: insets.bottom + 32 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <Text style={styles.link}>Volver</Text>
      </Pressable>
      <Text style={styles.title}>Compartir mi ubicación</Text>
      {sharing.isLoading ? <View style={styles.skeleton} /> : null}
      {sharing.isError ? (
        <View style={styles.card}>
          <Text style={styles.body}>No pudimos cargar esta configuración.</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void sharing.refetch()} style={styles.back}>
            <Text style={styles.link}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}
      {data ? (
        <>
          <Text style={styles.body}>
            {data.enabled
              ? 'Tus amigos ven un punto aproximado solo mientras la app está abierta.'
              : 'Hoy no compartes tu ubicación. El interruptor nace apagado.'}
          </Text>
          {data.hiddenBecauseAnonymous ? <Text style={styles.body}>Tienes una publicación anónima activa. Tu avatar no aparece en el mapa hasta que esa publicación termine.</Text> : null}
          {data.reminderDue ? (
            <View style={styles.card}>
              <Text style={styles.body}>Sigue activo compartir ubicación con tus amigos. Si ya no quieres, apágalo.</Text>
              <Pressable accessibilityRole="button" accessibilityLabel="Entendido" onPress={() => void api.acknowledgeLocationReminder().then(refresh)} style={styles.button}>
                <Text style={styles.buttonLabel}>Entendido</Text>
              </Pressable>
            </View>
          ) : null}
          {locked ? (
            <Text style={styles.body}>Como eres menor de edad, compartir ubicación solo se activa si tu representante ya lo autorizó. Pídele que lo marque en la autorización.</Text>
          ) : null}
          {!data.consentGranted ? (
            <View style={styles.card}>
              <Text style={styles.heading}>Consentimiento</Text>
              <Text style={styles.body}>{data.consentText}</Text>
              <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: accepted }} accessibilityLabel="Acepto compartir mi ubicación" onPress={() => setAccepted((value) => !value)} style={styles.back}>
                <Text style={styles.body}>{accepted ? '☑ Acepto este texto' : '☐ Acepto este texto'}</Text>
              </Pressable>
            </View>
          ) : null}
          <View style={styles.row}>
            {(['AllFriends', 'SelectedFriends'] as const).map((mode) => (
              <Pressable key={mode} accessibilityRole="button" accessibilityState={{ selected: data.mode === mode }} accessibilityLabel={mode === 'AllFriends' ? 'Todos los amigos' : 'Amigos elegidos'} disabled={pending || locked} onPress={() => void save({ enabled: true, mode, friendIds: mode === 'SelectedFriends' ? selected : undefined })} style={styles.chip}>
                <Text style={styles.body}>{mode === 'AllFriends' ? 'Todos los amigos' : 'Amigos elegidos'}</Text>
              </Pressable>
            ))}
          </View>
          {data.mode === 'SelectedFriends' ? (
            <View style={styles.card}>
              {(friends.data?.friends ?? []).map((friend) => {
                const on = selected.includes(friend.userId);
                return (
                  <Pressable key={friend.userId} accessibilityRole="checkbox" accessibilityState={{ checked: on }} accessibilityLabel={friend.displayName} onPress={() => {
                    const next = on ? selected.filter((id) => id !== friend.userId) : [...selected, friend.userId];
                    setPicked(next);
                    void save({ enabled: data.enabled, mode: 'SelectedFriends', friendIds: next });
                  }} style={styles.back}>
                    <Text style={styles.body}>{`${on ? '☑' : '☐'} ${friend.displayName}`}</Text>
                  </Pressable>
                );
              })}
            </View>
          ) : null}
          <Text style={styles.heading}>Duración</Text>
          <View style={styles.row}>
            {([['1h', '1 hora'], ['8h', '8 horas'], ['until_off', 'Hasta apagar']] as const).map(([duration, label]) => (
              <Pressable key={duration} accessibilityRole="button" accessibilityLabel={label} disabled={pending || locked} onPress={() => void save({ enabled: true, duration })} style={styles.chip}>
                <Text style={styles.body}>{label}</Text>
              </Pressable>
            ))}
          </View>
          <Text style={styles.heading}>Modo invisible</Text>
          <Text style={styles.body}>{data.ghost ? 'Estás invisible. Nadie ve tu punto hasta que termine.' : 'Invisible oculta tu punto sin apagar el interruptor.'}</Text>
          <View style={styles.row}>
            {([['1h', '1 hora'], ['8h', '8 horas'], ['tomorrow', 'Hasta mañana'], ['off', 'Quitar']] as const).map(([ghost, label]) => (
              <Pressable key={ghost} accessibilityRole="button" accessibilityLabel={`Invisible ${label}`} disabled={pending} onPress={() => void save({ enabled: data.enabled, ghost })} style={styles.chip}>
                <Text style={styles.body}>{label}</Text>
              </Pressable>
            ))}
          </View>
          <Pressable accessibilityRole="switch" accessibilityState={{ checked: data.enabled }} accessibilityLabel={data.enabled ? 'Dejar de compartir' : 'Compartir con amigos'} disabled={pending || (locked && !data.enabled)} onPress={() => void save({ enabled: !data.enabled, friendIds: data.mode === 'SelectedFriends' ? selected : undefined })} style={styles.button}>
            <Text style={styles.buttonLabel}>{pending ? 'Guardando…' : data.enabled ? 'Dejar de compartir' : 'Compartir con amigos'}</Text>
          </Pressable>
          {!data.isMinor ? (
            <View style={styles.card}>
              <Text style={styles.heading}>Mapa general</Text>
              <Text style={styles.body}>{data.profilePublic ? 'Quien tenga cuenta puede ver un punto mucho menos preciso.' : 'Solo un perfil público puede aparecer en el mapa general.'}</Text>
              {!data.publicConsentGranted ? <Text style={styles.body}>{data.publicConsentText}</Text> : null}
              {!data.publicConsentGranted ? (
                <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: publicAccepted }} accessibilityLabel="Acepto mostrar mi ubicación en el mapa" onPress={() => setPublicAccepted((value) => !value)} style={styles.back}>
                  <Text style={styles.body}>{publicAccepted ? '☑ Acepto este texto' : '☐ Acepto este texto'}</Text>
                </Pressable>
              ) : null}
              <View style={styles.row}>
                {([['Realtime', 'Tiempo real'], ['Approximate', 'Aproximada'], ['Hidden', 'Oculta']] as const).map(([mode, label]) => (
                  <Pressable key={mode} accessibilityRole="button" accessibilityState={{ selected: data.publicMode === mode }} accessibilityLabel={label} disabled={pending} onPress={() => void savePublic(mode)} style={styles.chip}>
                    <Text style={styles.body}>{label}</Text>
                  </Pressable>
                ))}
              </View>
            </View>
          ) : (
            <Text style={styles.body}>Tu ubicación no se muestra en el mapa general.</Text>
          )}
          <Text style={styles.heading}>Zonas de privacidad</Text>
          <Text style={styles.body}>En casa o en el trabajo puedes ocultar el punto o mostrarlo mucho más amplio.</Text>
          {Platform.OS === 'web' ? <Text style={styles.body}>El mapa para marcar la zona está en la app de iOS y Android.</Text> : (
            <MapView style={styles.map} provider={PROVIDER_DEFAULT} initialRegion={bogota} onRegionChangeComplete={(region) => setCenter({ latitude: region.latitude, longitude: region.longitude })}>
              <Marker coordinate={center} />
            </MapView>
          )}
          <View style={styles.row}>
            {(['Casa', 'Trabajo', 'Otro'] as const).map((name) => (
              <Pressable key={name} accessibilityRole="button" accessibilityState={{ selected: zoneName === name }} accessibilityLabel={name} onPress={() => setZoneName(name)} style={styles.chip}>
                <Text style={styles.body}>{name}</Text>
              </Pressable>
            ))}
          </View>
          <View style={styles.row}>
            {([100, 300, 1000] as const).map((radius) => (
              <Pressable key={radius} accessibilityRole="button" accessibilityState={{ selected: zoneRadius === radius }} accessibilityLabel={`${radius} metros`} onPress={() => setZoneRadius(radius)} style={styles.chip}>
                <Text style={styles.body}>{`${radius} m`}</Text>
              </Pressable>
            ))}
          </View>
          <View style={styles.row}>
            <Pressable accessibilityRole="button" accessibilityState={{ selected: zoneMode === 'Hidden' }} accessibilityLabel="Ocultar" onPress={() => setZoneMode('Hidden')} style={styles.chip}>
              <Text style={styles.body}>Ocultar</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityState={{ selected: zoneMode === 'Approximate' }} accessibilityLabel="Aproximada" onPress={() => setZoneMode('Approximate')} style={styles.chip}>
              <Text style={styles.body}>Aproximada</Text>
            </Pressable>
          </View>
          <Pressable accessibilityRole="button" accessibilityLabel="Guardar zona" disabled={pending} onPress={() => void addZone()} style={styles.button}>
            <Text style={styles.buttonLabel}>Guardar esta zona</Text>
          </Pressable>
          {(zones.data?.zones ?? []).map((zone) => (
            <View key={zone.id} style={styles.card}>
              <Text style={styles.body}>{`${zone.name} · ${zone.radiusMeters} m · ${zone.mode === 'Hidden' ? 'oculta' : 'aproximada'}`}</Text>
              <Pressable accessibilityRole="button" accessibilityLabel={`Quitar ${zone.name}`} onPress={() => void api.deletePrivacyZone(zone.id).then(refresh)} style={styles.back}>
                <Text style={styles.link}>Quitar</Text>
              </Pressable>
            </View>
          ))}
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 20 },
  back: { minHeight: 44, justifyContent: 'center' },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  heading: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 20, marginTop: 8 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  card: { gap: 8, padding: 16, borderRadius: 12, backgroundColor: theme.colors.surface },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: { minHeight: 44, paddingHorizontal: 12, borderRadius: 12, backgroundColor: theme.colors.surface, justifyContent: 'center' },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  skeleton: { height: 120, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  map: { height: 220, borderRadius: 12 },
});
