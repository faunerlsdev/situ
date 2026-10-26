import {
  absoluteMediaUrl,
  alertModal,
  AppError,
  classifyWriteError,
  asNumber,
  categoriesUnder,
  categoryHasChildren,
  defaultApiUrl,
  feedbackMessages,
  formatDistance,
  mapRealtimeOf,
  notify,
  panelCopyOf,
  publishBlock,
  readApiUrl,
  relativeTime,
  rollbackReaction,
  toggleReaction,
  toMapMarkers,
  zoomFromLongitudeDelta,
} from '@situ/shared';
import type { AuthorPresentation, ReportCategory } from '@situ/shared';
import BottomSheet, { BottomSheetScrollView } from '@gorhom/bottom-sheet';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { useIsFocused, useRouter } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import * as Location from 'expo-location';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import {
  AccessibilityInfo,
  ActivityIndicator,
  Animated,
  Image,
  Linking,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import MapView, { Marker, PROVIDER_DEFAULT, PROVIDER_GOOGLE } from 'react-native-maps';
import type { Region } from 'react-native-maps';

import { blockContent } from '@/features/interaction/block-content';
import { ComplaintSheet } from '@/features/interaction/complaint-sheet';
import { ReactionBar } from '@/features/interaction/reaction-bar';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

import { setMapFilters, useMapFilters } from './filters';
import { setMapFocus, useMapFocus } from './focus';
import { MapErrorBoundary, MapUnavailable } from './map-unavailable';
import { useMapProvider } from './map-provider';
import { setMapPlace, useMapPlace } from './place';
import { useMapRealtime } from './realtime/use-map-realtime';
import { useAppConfig } from './use-app-config';
import { useMe } from './use-me';
import { useReportCatalog } from './use-catalog';

const closeIcon = { ios: 'xmark', android: 'close', web: 'close' } as const;
const commentIcon = { ios: 'bubble.left', android: 'chat', web: 'chat' } as const;
const shareIcon = { ios: 'square.and.arrow.up', android: 'share', web: 'share' } as const;
const flagIcon = { ios: 'flag', android: 'flag', web: 'flag' } as const;
const blockIcon = { ios: 'nosign', android: 'block', web: 'block' } as const;

const pin = require('../../assets/images/icons/situ-pin-mapa-dark.png');
const isotipo = require('../../assets/images/brand/situ-isotipo-negro.png');
const avatar = require('../../assets/images/brand/situ-isotipo-naranja-claro.png');
const darkMap = require('../../mapStyles/dark.json');

const bogota = { latitude: 4.711, longitude: -74.072 };
const apiBase = readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl);

export function MapScreen() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const catalog = useReportCatalog();
  const me = useMe();
  const appConfig = useAppConfig();
  const copy = panelCopyOf(appConfig.data);
  const homeRef = useRef(bogota);
  const homeCity = me.data?.homeCity;
  if (homeCity) {
    homeRef.current = {
      latitude: asNumber(homeCity.latitude),
      longitude: asNumber(homeCity.longitude),
    };
  }
  const filters = useMapFilters();
  const focus = useMapFocus();
  const place = useMapPlace();
  const mapChoice = useMapProvider();
  const sheetRef = useRef<BottomSheet>(null);
  const mapRef = useRef<MapView>(null);
  const userRef = useRef<{ latitude: number; longitude: number } | null>(null);
  const [region, setRegion] = useState<Region | null>(null);
  const [settled, setSettled] = useState<Region | null>(null);
  const [follow, setFollow] = useState(false);
  const [user, setUser] = useState<{ latitude: number; longitude: number } | null>(null);
  userRef.current = user;
  const [gate, setGate] = useState<'checking' | 'explain' | 'ready'>('checking');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [sheetIndex, setSheetIndex] = useState(0);
  const [parentId, setParentId] = useState<string | null>(null);
  const [reduceMotion, setReduceMotion] = useState(false);
  const [mapAttempt, setMapAttempt] = useState(0);
  const [complaintId, setComplaintId] = useState<string | null>(null);
  const [complaintPending, setComplaintPending] = useState(false);
  const [duplicate, setDuplicate] = useState<string | null>(null);
  const debounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion);
  }, []);

  useEffect(() => {
    let cancelled = false;
    async function prepare() {
      const current = await Location.getForegroundPermissionsAsync();
      if (cancelled) {
        return;
      }
      if (current.granted) {
        await centerOnUser();
        setGate('ready');
        return;
      }
      setGate('explain');
    }
    void prepare();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!follow || !user || !mapRef.current) {
      return;
    }
    mapRef.current.animateToRegion({ ...user, latitudeDelta: 0.02, longitudeDelta: 0.02 }, reduceMotion ? 0 : 400);
  }, [follow, reduceMotion, user]);

  useEffect(() => {
    if (gate !== 'ready') {
      return;
    }
    let subscription: Location.LocationSubscription | null = null;
    let cancelled = false;
    void Location.watchPositionAsync(
      { accuracy: Location.Accuracy.Balanced, distanceInterval: 30 },
      (position) => setUser({ latitude: position.coords.latitude, longitude: position.coords.longitude }),
    ).then((next) => {
      if (cancelled) {
        next.remove();
        return;
      }
      subscription = next;
    }).catch(() => undefined);
    return () => {
      cancelled = true;
      subscription?.remove();
    };
  }, [gate]);

  useEffect(() => {
    if (!focus || !mapRef.current) {
      return;
    }
    setSelectedId(focus.id);
    mapRef.current.animateToRegion({ latitude: focus.latitude, longitude: focus.longitude, latitudeDelta: 0.01, longitudeDelta: 0.01 }, reduceMotion ? 0 : 450);
    setMapFocus(null);
  }, [focus, reduceMotion]);

  useEffect(() => {
    if (!place) {
      return;
    }
    const next = { latitude: place.latitude, longitude: place.longitude, latitudeDelta: 0.02, longitudeDelta: 0.02 };
    setFollow(false);
    setRegion(next);
    setSettled(next);
    mapRef.current?.animateToRegion(next, reduceMotion ? 0 : 400);
    setMapPlace(null);
  }, [place, reduceMotion]);

  const zoom = settled ? zoomFromLongitudeDelta(settled.longitudeDelta) : 14;
  const mapQuery = useQuery({
    queryKey: ['reports-map', settled, filters],
    enabled: settled !== null,
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) => {
      if (!settled) {
        throw new Error('region');
      }
      return api.mapReports({
        minLat: settled.latitude - settled.latitudeDelta / 2,
        maxLat: settled.latitude + settled.latitudeDelta / 2,
        minLon: settled.longitude - settled.longitudeDelta / 2,
        maxLon: settled.longitude + settled.longitudeDelta / 2,
        zoom,
        categories: filters.categoryCodes,
        since: filters.since === 'all' ? null : filters.since,
        friendsOnly: filters.friendsOnly,
        latitude: userRef.current?.latitude ?? null,
        longitude: userRef.current?.longitude ?? null,
      }, signal);
    },
  });
  const showBar = useDelayed(mapQuery.isFetching, 300);
  const focused = useIsFocused();
  const grid = mapRealtimeOf(appConfig.data);
  const realtime = useMapRealtime({
    mapVisible: focused && gate === 'ready',
    region: settled,
    cellDegrees: grid.cellDegrees,
    maxCells: grid.maxCells,
    onResync: () => {
      void mapQuery.refetch();
    },
    onRemove: (ids) => {
      setSelectedId((current) => (current && ids.includes(current) ? null : current));
    },
  });

  useEffect(() => {
    if (!mapQuery.isError || mapQuery.isFetching) {
      return;
    }
    notify.error(mapQuery.error, { action: { label: 'Reintentar', onPress: () => void mapQuery.refetch() } });
  }, [mapQuery.errorUpdatedAt]);

  const markers = toMapMarkers(mapQuery.data?.items ?? []);
  const categories = catalog.data?.categories ?? [];
  const selected = markers.find((item) => item.kind === 'point' && item.id === selectedId);
  const selectedPoint = selected?.kind === 'point' ? selected : null;
  const detail = useQuery({
    queryKey: ['report', selectedId],
    enabled: selectedId !== null,
    queryFn: () => api.report(selectedId ?? ''),
  });

  const onRegion = useCallback((next: Region) => {
    setRegion(next);
    if (debounce.current) {
      clearTimeout(debounce.current);
    }
    debounce.current = setTimeout(() => setSettled(next), 500);
  }, []);

  async function allowLocation() {
    const permission = await Location.requestForegroundPermissionsAsync();
    if (!permission.granted) {
      await alertModal({
        title: feedbackMessages.locationDeniedTitle,
        message: feedbackMessages.locationDeniedBody,
        type: 'warning',
        buttonText: feedbackMessages.openSettings,
      });
      await Linking.openSettings();
      move(homeRef.current, false);
      setGate('ready');
      return;
    }
    await centerOnUser();
    setGate('ready');
  }

  async function centerOnUser() {
    try {
      const position = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
      const next = { latitude: position.coords.latitude, longitude: position.coords.longitude };
      setUser(next);
      move(next, true);
    } catch {
      move(homeRef.current, false);
    }
  }

  async function openPublish() {
    if (me.isPending) {
      return;
    }

    const block = publishBlock(me.data?.canPublish === true, me.data?.publishBlockedReason);
    if (!me.data || block.kind === 'allowed') {
      sheetRef.current?.snapToIndex(1);
      return;
    }

    if (block.kind === 'confirm-email') {
      await alertModal({
        title: 'Confirma tu correo para publicar',
        message: 'Te enviamos un enlace cuando creaste la cuenta. Podemos enviar otro.',
        type: 'warning',
        buttonText: 'Reenviar correo',
      });
      await api.resendMyConfirmationEmail().catch((cause: unknown) => notify.error(cause));
      return;
    }

    if (block.kind === 'guardian') {
      await alertModal({
        title: 'Falta la autorización',
        message: 'Tu representante todavía no autoriza la cuenta.',
        type: 'warning',
        buttonText: 'Continuar',
      });
      router.push('/esperando-autorizacion');
      return;
    }

    await alertModal({ title: 'No puedes publicar', message: block.message, type: 'warning' });
  }

  function move(center: { latitude: number; longitude: number }, tracking: boolean) {
    const next = { ...center, latitudeDelta: 0.05, longitudeDelta: 0.05 };
    setFollow(tracking);
    setRegion(next);
    setSettled(next);
    mapRef.current?.animateToRegion(next, reduceMotion ? 0 : 400);
  }

  async function vote(type: 'StillThere' | 'GoneNow') {
    if (!selectedId) {
      return;
    }
    if (!user) {
      await alertModal({ title: 'Necesitamos tu ubicación', message: 'Activa la ubicación para confirmar lo que ves en el lugar.', type: 'warning' });
      return;
    }
    try {
      await api.confirmReport(selectedId, { type, latitude: user.latitude, longitude: user.longitude });
      await queryClient.invalidateQueries({ queryKey: ['report', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['reports-map'] });
      notify.success(type === 'StillThere' ? 'Marcaste que sigue ahí.' : 'Marcaste que ya no está.');
    } catch (error) {
      if (error instanceof AppError && (error.apiCode === 'report.confirmation_too_far' || error.apiCode === 'report.confirmation_own')) {
        await alertModal({ title: 'No se pudo confirmar', message: error.userMessage, type: 'warning' });
        return;
      }
      notify.error(error, { action: { label: 'Reintentar', onPress: () => void vote(type) } });
    }
  }

  async function reactToReport(id: string, type: string) {
    const current = detail.data;
    if (!current || current.report.id !== id) {
      return;
    }
    const snapshot = {
      counts: current.report.reactionCounts.map((item) => ({ type: item.type, count: Number(item.count) })),
      mine: current.report.myReaction,
    };
    const next = toggleReaction(snapshot, type);
    queryClient.setQueryData<typeof current>(['report', id], (value) => value ? {
      ...value,
      report: { ...value.report, reactionCounts: next.counts, myReaction: next.mine },
    } : value);
    try {
      if (next.mine === null) {
        await api.clearReportReaction(id);
      } else {
        await api.setReportReaction(id, next.mine);
      }
    } catch (error) {
      queryClient.setQueryData(['report', id], current);
      void rollbackReaction(snapshot);
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void reactToReport(id, type) } });
    }
  }

  async function submitComplaint(reason: Parameters<typeof api.submitComplaint>[0]['reason'], details: string) {
    if (!complaintId) {
      return;
    }
    setComplaintPending(true);
    try {
      await api.submitComplaint({ targetType: 'Report', targetId: complaintId, reason, details: details || null });
      setComplaintId(null);
      setDuplicate(null);
      notify.success(feedbackMessages.complaintThanks);
    } catch (error) {
      const failure = classifyWriteError(error instanceof AppError ? error : { code: 'UNKNOWN' });
      if (failure.kind === 'duplicate') {
        setDuplicate(failure.message);
        return;
      }
      notify.error(error);
    } finally {
      setComplaintPending(false);
    }
  }

  if (gate !== 'ready') {
    return (
      <View style={styles.gate}>
        {gate === 'checking' ? <ActivityIndicator color={theme.colors.primary} /> : (
          <>
            <Text style={styles.gateTitle}>Para mostrarte lo que pasa cerca</Text>
            <Text style={styles.gateBody}>SITU usa tu ubicación para centrar el mapa, calcular distancias y confirmar que estás cerca de un reporte. Si no la concedes, el mapa sigue disponible en tu ciudad.</Text>
            <Pressable accessibilityRole="button" accessibilityLabel="Continuar" onPress={() => void allowLocation()} style={styles.gateButton}>
              <Text style={styles.gateButtonLabel}>Continuar</Text>
            </Pressable>
          </>
        )}
      </View>
    );
  }

  const visibleCategories = categoriesUnder(categories, parentId);

  return (
    <View style={styles.fill}>
      {showBar ? <View style={styles.bar} /> : null}
      {realtime.mode === 'live' || realtime.mode === 'down' ? (
        <View
          accessibilityRole="text"
          accessibilityLabel={realtime.mode === 'live' ? 'Mapa en tiempo real' : 'Sin conexión en tiempo real'}
          style={styles.live}
        >
          <View style={[styles.liveDot, realtime.mode === 'down' && styles.liveDotDown]} />
          {realtime.mode === 'down' ? <Text style={styles.liveText}>Sin conexión en tiempo real</Text> : null}
        </View>
      ) : null}
      <FilterBar categories={categories} />
      {Platform.OS === 'web' ? (
        <View style={styles.gate}><Text style={styles.gateBody}>El mapa nativo está en la app de iOS y Android.</Text></View>
      ) : mapChoice.status === 'unavailable' ? (
        <MapUnavailable key={mapAttempt} missingVariable={mapChoice.missingVariable} onRetry={() => setMapAttempt((value) => value + 1)} />
      ) : (
        <MapErrorBoundary>
          <MapView
          ref={mapRef}
          style={styles.fill}
          provider={mapChoice.status === 'google' ? PROVIDER_GOOGLE : PROVIDER_DEFAULT}
          customMapStyle={mapChoice.status === 'google' ? darkMap : undefined}
          userInterfaceStyle={mapChoice.status === 'apple' ? mapChoice.userInterfaceStyle : undefined}
          initialRegion={region ?? { ...bogota, latitudeDelta: 0.05, longitudeDelta: 0.05 }}
          showsUserLocation={user !== null}
          onPanDrag={() => setFollow(false)}
          onRegionChangeComplete={onRegion}
        >
          {markers.map((item) => (
            <Marker
              key={item.kind === 'cluster' ? item.key : item.id}
              coordinate={{ latitude: item.latitude, longitude: item.longitude }}
              tracksViewChanges={item.kind === 'point' && realtime.freshIds.has(item.id)}
              accessibilityLabel={item.kind === 'cluster' ? `${item.count} reportes` : item.categoryCode ?? 'Reporte'}
              onPress={() => {
                if (item.kind === 'cluster') {
                  const current = region ?? settled;
                  mapRef.current?.animateToRegion({
                    latitude: item.latitude,
                    longitude: item.longitude,
                    latitudeDelta: (current?.latitudeDelta ?? 0.05) / 2,
                    longitudeDelta: (current?.longitudeDelta ?? 0.05) / 2,
                  }, reduceMotion ? 0 : 350);
                  return;
                }
                setSelectedId(item.id);
              }}
            >
              {item.kind === 'cluster' ? (
                <View style={styles.cluster}><Text style={styles.clusterText}>{item.count}</Text></View>
              ) : (
                <PointPin fresh={realtime.freshIds.has(item.id)} reduceMotion={reduceMotion}>
                  <CategoryPin code={item.categoryCode} categories={categories} />
                  {item.isMine ? <Text style={styles.mine}>Tuyo</Text> : null}
                </PointPin>
              )}
            </Marker>
          ))}
          </MapView>
        </MapErrorBoundary>
      )}
      {selectedPoint && sheetIndex === 0 ? (
        <View style={styles.card}>
          <View style={styles.cardHead}>
            <Pressable accessibilityRole="button" accessibilityLabel="Ver detalle del reporte" onPress={() => router.push({ pathname: '/reporte/[id]', params: { id: selectedPoint.id } })} style={styles.cardBody}>
              <Text style={styles.cardTitle}>{categoryName(categories, selectedPoint.categoryCode)}</Text>
              <Text style={styles.cardMeta}>{relativeTime(selectedPoint.createdAt ?? '')}{formatDistance(selectedPoint.distanceMeters) ? ` · ${formatDistance(selectedPoint.distanceMeters)}` : ''}</Text>
              <AuthorLine author={selectedPoint.author} />
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel="Cerrar" onPress={() => setSelectedId(null)} style={styles.close}>
              <SymbolView name={closeIcon} tintColor={theme.colors.warning} size={18} />
            </Pressable>
          </View>
          <MediaThumbs media={detail.data?.media ?? []} fallbackUrl={selectedPoint.previewUrl} />
          <View style={styles.actions}>
            <VoteButton label="Sigue ahí" selected={detail.data?.myConfirmation === 'StillThere'} onPress={() => void vote('StillThere')} />
            <VoteButton label="Ya no está" selected={detail.data?.myConfirmation === 'GoneNow'} onPress={() => void vote('GoneNow')} />
          </View>
          {detail.data ? (
            <ReactionBar
              counts={detail.data.report.reactionCounts.map((item) => ({ type: item.type, count: Number(item.count) }))}
              mine={detail.data.report.myReaction}
              onToggle={(type) => void reactToReport(selectedPoint.id, type)}
            />
          ) : null}
          <View style={styles.iconActions}>
            <Pressable accessibilityRole="button" accessibilityLabel="Comentar" onPress={() => router.push({ pathname: '/reporte/[id]', params: { id: selectedPoint.id } })} style={styles.iconAction}>
              <SymbolView name={commentIcon} tintColor={theme.colors.textPrimary} size={22} />
            </Pressable>
            <View accessibilityLabel="Compartir, próximamente" style={[styles.iconAction, styles.iconSoon]}>
              <SymbolView name={shareIcon} tintColor={theme.colors.textSecondary} size={22} />
            </View>
            <Pressable accessibilityRole="button" accessibilityLabel="Denunciar" onPress={() => { setDuplicate(null); setComplaintId(selectedPoint.id); }} style={styles.iconAction}>
              <SymbolView name={flagIcon} tintColor={theme.colors.textPrimary} size={22} />
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel="Bloquear" onPress={() => void blockContent(queryClient, { anonymous: selectedPoint.isAnonymous, reportId: selectedPoint.id, userId: detail.data?.report.author.userId })} style={styles.iconAction}>
              <SymbolView name={blockIcon} tintColor={theme.colors.danger} size={22} />
            </Pressable>
          </View>
        </View>
      ) : null}
      <ComplaintSheet
        visible={complaintId !== null}
        pending={complaintPending}
        duplicateMessage={duplicate}
        animationType={reduceMotion ? 'none' : 'slide'}
        onClose={() => { setComplaintId(null); setDuplicate(null); }}
        onSubmit={(reason, details) => void submitComplaint(reason, details)}
      />
      <Pressable accessibilityRole="button" accessibilityLabel="¿Dónde estoy?" onPress={() => void centerOnUser()} style={styles.locate}>
        <View style={styles.locateDot} />
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Quiero reportar algo" onPress={() => void openPublish()} style={styles.fab}>
        <Image source={isotipo} resizeMode="contain" style={styles.fabMark} />
      </Pressable>
      {/* Extensión: dictado por voz al final del MVP y "Transmitir en vivo" en la Fase 2. No van en esta UI. */}
      <BottomSheet
        ref={sheetRef}
        index={0}
        snapPoints={[88, '46%', '88%']}
        onChange={setSheetIndex}
        enableDynamicSizing={false}
        animateOnMount={!reduceMotion}
        backgroundStyle={{ backgroundColor: theme.colors.surface }}
        handleIndicatorStyle={{ backgroundColor: theme.colors.border }}
      >
        <Text style={styles.sheetTitle}>{copy.browse}</Text>
        <Text style={styles.sheetSubtitle}>{copy.report}</Text>
        <BottomSheetScrollView contentContainerStyle={styles.grid}>
          {parentId ? (
            <Pressable accessibilityRole="button" accessibilityLabel="Volver a las categorías" onPress={() => setParentId(null)} style={styles.back}>
              <Text style={styles.backLabel}>Volver</Text>
            </Pressable>
          ) : null}
          {catalog.isLoading && categories.length === 0 ? <SkeletonGrid /> : null}
          {catalog.isError && categories.length === 0 ? (
            <ErrorState onRetry={() => void catalog.refetch()} />
          ) : visibleCategories.map((category) => (
            <Pressable
              key={category.id}
              accessibilityRole="button"
              accessibilityLabel={category.name}
              onPress={() => {
                if (categoryHasChildren(categories, category.id)) {
                  setParentId(category.id);
                  return;
                }
                router.push({ pathname: '/reportar', params: { categoryId: category.id } });
              }}
              style={styles.cell}
            >
              <CategoryIcon uri={absoluteMediaUrl(apiBase, category.buttonIconUrl)} />
              <Text style={styles.cellTitle}>{category.name}</Text>
              <Text style={styles.cellBody} numberOfLines={2}>{category.description}</Text>
            </Pressable>
          ))}
        </BottomSheetScrollView>
      </BottomSheet>
    </View>
  );
}

function FilterBar({ categories }: { categories: ReportCategory[] }) {
  const filters = useMapFilters();
  const roots = categoriesUnder(categories, null);
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filters} contentContainerStyle={styles.filterContent}>
      {roots.map((category) => {
        const active = filters.categoryCodes.includes(category.code);
        return (
          <Pressable
            key={category.id}
            accessibilityRole="button"
            accessibilityState={{ selected: active }}
            accessibilityLabel={category.name}
            onPress={() => setMapFilters({
              ...filters,
              categoryCodes: active ? filters.categoryCodes.filter((code) => code !== category.code) : [...filters.categoryCodes, category.code],
            })}
            style={[styles.chip, active && styles.chipOn]}
          >
            <Text style={styles.chipLabel}>{category.name}</Text>
          </Pressable>
        );
      })}
      {(['all', 'hour', 'today'] as const).map((since) => (
        <Pressable key={since} accessibilityRole="button" accessibilityState={{ selected: filters.since === since }} accessibilityLabel={sinceLabel(since)} onPress={() => setMapFilters({ ...filters, since })} style={[styles.chip, filters.since === since && styles.chipOn]}>
          <Text style={styles.chipLabel}>{sinceLabel(since)}</Text>
        </Pressable>
      ))}
      <Pressable accessibilityRole="button" accessibilityState={{ selected: filters.friendsOnly }} accessibilityLabel="Solo amigos" onPress={() => setMapFilters({ ...filters, friendsOnly: !filters.friendsOnly })} style={[styles.chip, filters.friendsOnly && styles.chipOn]}>
        <Text style={styles.chipLabel}>Solo amigos</Text>
      </Pressable>
    </ScrollView>
  );
}

function PointPin({ fresh, reduceMotion, children }: { fresh: boolean; reduceMotion: boolean; children: ReactNode }) {
  const opacity = useRef(new Animated.Value(fresh && !reduceMotion ? 0 : 1)).current;
  useEffect(() => {
    if (!fresh || reduceMotion) {
      opacity.setValue(1);
      return;
    }
    Animated.timing(opacity, { toValue: 1, duration: 280, useNativeDriver: true }).start();
  }, [fresh, opacity, reduceMotion]);
  return <Animated.View style={{ opacity }}>{children}</Animated.View>;
}

function CategoryPin({ code, categories }: { code: string | null; categories: ReportCategory[] }) {
  const category = categories.find((item) => item.code === code);
  const [failed, setFailed] = useState(false);
  const uri = category ? absoluteMediaUrl(apiBase, category.mapIconUrl) : null;
  if (!uri || failed) {
    return <Image source={pin} style={styles.pin} resizeMode="contain" />;
  }
  return <Image source={{ uri }} style={styles.pin} resizeMode="contain" onError={() => setFailed(true)} />;
}

function CategoryIcon({ uri }: { uri: string | null }) {
  const [failed, setFailed] = useState(false);
  if (!uri || failed) {
    return <Image source={pin} style={styles.icon} resizeMode="contain" />;
  }
  return <Image source={{ uri }} style={styles.icon} resizeMode="contain" onError={() => setFailed(true)} />;
}

function VoteButton({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected }} accessibilityLabel={label} onPress={onPress} style={[styles.vote, selected && styles.voteOn]}>
      <Text style={styles.voteLabel}>{label}</Text>
    </Pressable>
  );
}

function SkeletonGrid() {
  return (
    <View style={styles.grid}>
      {[0, 1, 2, 3].map((item) => <View key={item} style={styles.skeleton} />)}
    </View>
  );
}

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <View style={styles.errorBox}>
      <Image source={avatar} style={styles.icon} resizeMode="contain" />
      <Text style={styles.cardMeta}>No pudimos cargar las categorías.</Text>
      <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={onRetry} style={styles.vote}>
        <Text style={styles.voteLabel}>Reintentar</Text>
      </Pressable>
    </View>
  );
}

function sinceLabel(since: 'all' | 'hour' | 'today'): string {
  if (since === 'hour') return 'Última hora';
  if (since === 'today') return 'Hoy';
  return 'Todo';
}

function categoryName(categories: ReportCategory[], code: string | null): string {
  return categories.find((category) => category.code === code)?.name ?? 'Reporte';
}

function useDelayed(active: boolean, delay: number): boolean {
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    if (!active) {
      setVisible(false);
      return;
    }
    const timer = setTimeout(() => setVisible(true), delay);
    return () => clearTimeout(timer);
  }, [active, delay]);
  return visible;
}

function MediaThumbs({ media, fallbackUrl }: { media: { id: string; type: string; url: string; thumbnailUrl: string | null }[]; fallbackUrl: string | null }) {
  if (media.length === 0) {
    const preview = absoluteMediaUrl(apiBase, fallbackUrl);
    return preview ? <Image source={{ uri: preview }} style={styles.preview} /> : null;
  }

  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.thumbs}>
      {media.map((item) => {
        const uri = absoluteMediaUrl(apiBase, item.thumbnailUrl ?? (item.type === 'Video' ? null : item.url));
        return (
          <View key={item.id} style={styles.thumb}>
            {uri ? <Image source={{ uri }} style={styles.thumbImage} /> : null}
            {item.type === 'Video' ? <Text style={styles.thumbLabel}>Video</Text> : null}
          </View>
        );
      })}
    </ScrollView>
  );
}

function AuthorLine({ author }: { author: AuthorPresentation }) {
  return (
    <View style={styles.authorRow}>
      {author.anonymous ? <Image source={avatar} style={styles.avatar} /> : null}
      <View>
        <Text style={styles.cardAuthor}>{author.name}</Text>
        {author.memberSince ? <Text style={styles.cardMeta}>{author.memberSince}</Text> : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1, backgroundColor: theme.colors.mapWater },
  bar: { position: 'absolute', top: 0, left: 0, right: 0, height: 2, backgroundColor: theme.colors.primary, zIndex: 5 },
  live: { position: 'absolute', top: 64, left: 12, zIndex: 4, flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 8, paddingVertical: 4, borderRadius: 12, backgroundColor: theme.colors.surfaceElevated },
  liveDot: { width: 8, height: 8, borderRadius: 4, backgroundColor: theme.colors.success },
  liveDotDown: { backgroundColor: theme.colors.warning },
  liveText: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  gate: { flex: 1, justifyContent: 'center', gap: 16, padding: 24, backgroundColor: theme.colors.bg },
  gateTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  gateBody: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  gateButton: { minHeight: 48, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  gateButtonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  filters: { position: 'absolute', top: 8, left: 0, right: 0, zIndex: 4, maxHeight: 52 },
  filterContent: { paddingHorizontal: 12, gap: 8, alignItems: 'center' },
  chip: { minHeight: 44, paddingHorizontal: 12, borderRadius: 22, justifyContent: 'center', backgroundColor: theme.colors.surfaceElevated },
  chipOn: { backgroundColor: theme.colors.primary },
  chipLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  pin: { width: 32, height: 40 },
  mine: { marginTop: -6, textAlign: 'center', color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 10 },
  cluster: { width: 44, height: 44, borderRadius: 22, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  clusterText: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  card: { position: 'absolute', left: 16, right: 88, bottom: 168, gap: 8, padding: 12, borderRadius: 20, backgroundColor: theme.colors.surfaceElevated, zIndex: 3 },
  cardHead: { flexDirection: 'row', alignItems: 'flex-start', gap: 8 },
  cardBody: { flex: 1, gap: 4 },
  close: { minHeight: 44, minWidth: 44, alignItems: 'center', justifyContent: 'center' },
  cardTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  cardMeta: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  cardAuthor: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  authorRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  avatar: { width: 28, height: 28 },
  preview: { height: 92, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  thumbs: { gap: 8 },
  thumb: { width: 72, height: 72, borderRadius: 12, overflow: 'hidden', backgroundColor: theme.colors.skeleton, alignItems: 'center', justifyContent: 'center' },
  thumbImage: { width: 72, height: 72 },
  thumbLabel: { position: 'absolute', color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 11 },
  actions: { flexDirection: 'row', gap: 8 },
  vote: { flex: 1, minHeight: 44, borderRadius: theme.radius.button, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surface },
  voteOn: { backgroundColor: theme.colors.primary },
  voteLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 13 },
  iconActions: { flexDirection: 'row', gap: 8 },
  iconAction: { flex: 1, minHeight: 44, borderRadius: theme.radius.button, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surface },
  iconSoon: { opacity: 0.45 },
  locate: { position: 'absolute', left: 16, bottom: 108, width: 48, height: 48, borderRadius: 24, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surfaceElevated, zIndex: 3 },
  locateDot: { width: 14, height: 14, borderRadius: 7, borderWidth: 2, borderColor: theme.colors.primary },
  fab: { position: 'absolute', right: 16, bottom: 100, width: 64, height: 64, borderRadius: 32, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surfaceElevated, borderWidth: 2, borderColor: theme.colors.primary, zIndex: 3 },
  fabMark: { width: 36, height: 46 },
  sheetTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 20, paddingHorizontal: 16 },
  sheetSubtitle: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14, paddingHorizontal: 16, marginBottom: 8 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, padding: 16 },
  cell: { width: '47%', minHeight: 120, gap: 6, padding: 12, borderRadius: 16, borderWidth: 1, borderColor: theme.colors.border },
  cellTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  cellBody: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  icon: { width: 36, height: 36 },
  skeleton: { width: '47%', height: 120, borderRadius: 16, backgroundColor: theme.colors.skeleton },
  back: { minHeight: 44, justifyContent: 'center' },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  errorBox: { gap: 8, padding: 12 },
});
