import {
  alertModal,
  anonymityNotice,
  anonymityNoticeAccepted,
  anonymityStatus,
  AppError,
  asNumber,
  categoriesUnder,
  confirm,
  descriptionMaxLength,
  feedbackMessages,
  mediaLimitsOf,
  notify,
  publishBlock,
  rejectedMediaReason,
  retryAfterLabel,
  validateReportForm,
  withMinorVisibility,
} from '@situ/shared';
import type { ReportVisibility } from '@situ/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import * as ImagePicker from 'expo-image-picker';
import * as Location from 'expo-location';
import { useRouter } from 'expo-router';
import { File, Paths } from 'expo-file-system';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import MapView, { Marker, PROVIDER_DEFAULT } from 'react-native-maps';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

import { setMapFocus } from './focus';
import { useMapProvider } from './map-provider';
import { useAppConfig } from './use-app-config';
import { useMe } from './use-me';
import { useReportCatalog } from './use-catalog';
import { localFileUri, prepareImage, useUploads } from './use-uploads';

const draftFile = new File(Paths.document, 'report-draft.json');

type Draft = {
  categoryId: string;
  description: string;
  visibility: ReportVisibility;
  isAnonymous: boolean;
  latitude: number;
  longitude: number;
};

export function ReportForm({ categoryId }: { categoryId: string }) {
  const mapChoice = useMapProvider();
  const router = useRouter();
  const queryClient = useQueryClient();
  const catalog = useReportCatalog();
  const me = useMe();
  const appConfig = useAppConfig();
  const limits = mediaLimitsOf(appConfig.data);
  const maxLength = descriptionMaxLength(appConfig.data);
  const uploads = useUploads();
  const categories = catalog.data?.categories ?? [];
  const [chosenId, setChosenId] = useState(categoryId);
  const [description, setDescription] = useState('');
  const [visibility, setVisibility] = useState<ReportVisibility>('Public');
  const [anonymous, setAnonymous] = useState(true);
  const identityLocked = useRef(false);
  const [point, setPoint] = useState({ latitude: 4.711, longitude: -74.072 });
  const [picking, setPicking] = useState(false);
  const [errors, setErrors] = useState<{ description?: string; media?: string }>({});
  const [address, setAddress] = useState('');
  const category = categories.find((item) => item.id === chosenId) ?? null;
  const rule = withMinorVisibility({ isMinor: me.data?.isMinor === true, visibility, isAnonymous: anonymous });

  useEffect(() => {
    void Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced })
      .then((position) => setPoint({ latitude: position.coords.latitude, longitude: position.coords.longitude }))
      .catch(() => undefined);
  }, [categoryId]);

  useEffect(() => {
    let cancelled = false;
    void readDraft().then((draft) => {
      if (cancelled || identityLocked.current) {
        return;
      }
      if (draft && draft.categoryId === categoryId) {
        identityLocked.current = true;
        setDescription(draft.description);
        setVisibility(draft.visibility);
        setAnonymous(draft.isAnonymous);
        setPoint({ latitude: draft.latitude, longitude: draft.longitude });
        return;
      }
      if (me.data) {
        identityLocked.current = true;
        setAnonymous(me.data.profile.preferAnonymousReports !== false);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [categoryId, me.data]);

  useEffect(() => {
    if (rule.explain) {
      setAnonymous(true);
    }
  }, [rule.explain]);

  const readyImages = uploads.tasks.filter((task) => task.kind === 'image' && task.status === 'ready').length;
  const readyVideos = uploads.tasks.filter((task) => task.kind === 'video' && task.status === 'ready').length;
  const imageCount = uploads.tasks.filter((task) => task.kind === 'image' && task.status !== 'cancelled').length;
  const videoCount = uploads.tasks.filter((task) => task.kind === 'video' && task.status !== 'cancelled').length;

  const create = useMutation({
    meta: { silentError: true },
    mutationFn: () => api.createReport({
      categoryId: chosenId,
      description: description.trim(),
      latitude: point.latitude,
      longitude: point.longitude,
      visibility: rule.visibility,
      isAnonymous: rule.isAnonymous,
      mediaIds: uploads.tasks.flatMap((task) => task.mediaId ? [task.mediaId] : []),
    }),
    onSuccess: async (result) => {
      await clearDraft();
      await queryClient.invalidateQueries({ queryKey: ['reports-map'] });
      await queryClient.invalidateQueries({ queryKey: ['reports-mine'] });
      setMapFocus({ id: result.report.id, latitude: asNumber(result.report.latitude), longitude: asNumber(result.report.longitude) });
      notify.brand(feedbackMessages.published);
      router.back();
    },
  });

  async function submit() {
    if (create.isPending || uploads.tasks.some((task) => task.status === 'uploading' || task.status === 'queued')) {
      return;
    }
    const block = publishBlock(me.data?.canPublish === true, me.data?.publishBlockedReason);
    if (me.data && block.kind !== 'allowed') {
      await explainPublishBlock(block, router);
      return;
    }
    const nextErrors = validateReportForm({
      description,
      mediaRequirement: category?.mediaRequirement ?? 'None',
      readyImages,
      readyVideos,
      maxLength,
    });
    setErrors(nextErrors);
    if (nextErrors.description || nextErrors.media) {
      return;
    }
    try {
      await create.mutateAsync();
    } catch (error) {
      await handleSubmitError(error, () => void submit(), router, setErrors);
    }
  }

  async function addImage(source: 'library' | 'camera') {
    if (imageCount >= limits.maxImages) {
      notify.warning(rejectedMediaReason({ kind: 'image', mime: 'image/jpeg', name: 'foto.jpg', imageCount, videoCount, limits }) ?? 'Puedes subir hasta 5 fotos.');
      return;
    }
    if (source === 'camera' && !(await allowCamera())) {
      return;
    }
    setPicking(true);
    try {
      const picked = source === 'camera'
        ? await ImagePicker.launchCameraAsync({ mediaTypes: ['images'], quality: 0.8 })
        : await ImagePicker.launchImageLibraryAsync({
            mediaTypes: ['images'],
            quality: 0.8,
            preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
          });
      const asset = picked.assets?.[0];
      if (picked.canceled || !asset) {
        return;
      }
      const uri = await prepareImage(asset.uri, asset.width);
      uploads.enqueue({ id: `${Date.now()}`, kind: 'image', status: 'queued', progress: 0, uri, name: 'foto.jpg', mime: 'image/jpeg' });
    } catch {
      notify.warning(feedbackMessages.photoPrepareFailed);
    } finally {
      setPicking(false);
    }
  }

  async function addVideo(source: 'library' | 'camera') {
    if (videoCount >= limits.maxVideos) {
      notify.warning(limits.maxVideos === 1 ? 'Puedes subir un solo video.' : `Puedes subir hasta ${limits.maxVideos} videos.`);
      return;
    }
    if (source === 'camera' && !(await allowCamera())) {
      return;
    }
    setPicking(true);
    try {
      const picked = source === 'camera'
        ? await ImagePicker.launchCameraAsync({ mediaTypes: ['videos'], videoMaxDuration: limits.maxVideoSeconds })
        : await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['videos'], videoMaxDuration: limits.maxVideoSeconds });
      const asset = picked.assets?.[0];
      if (picked.canceled || !asset) {
        return;
      }
      const duration = asset.duration ? asset.duration / 1000 : null;
      const mime = asset.mimeType ?? 'video/mp4';
      const name = asset.fileName ?? 'video.mp4';
      const blocked = rejectedMediaReason({ kind: 'video', mime, name, durationSeconds: duration, imageCount, videoCount, limits });
      if (blocked) {
        notify.warning(blocked);
        return;
      }
      const uri = await localFileUri(asset.uri, 'mp4');
      uploads.enqueue({ id: `${Date.now()}`, kind: 'video', status: 'queued', progress: 0, uri, name: name.endsWith('.mp4') || name.endsWith('.mov') ? name : 'video.mp4', mime });
    } catch {
      notify.warning(feedbackMessages.videoPrepareFailed);
    } finally {
      setPicking(false);
    }
  }

  async function close() {
    const dirty = description.trim().length > 0 || uploads.tasks.length > 0;
    if (dirty) {
      const leave = await confirm({ title: 'Salir sin publicar', message: 'Guardamos el texto en este dispositivo por si quieres volver.', type: 'warning', confirmText: 'Salir', cancelText: 'Seguir' });
      if (!leave) {
        return;
      }
      await writeDraft({ categoryId: chosenId, description, visibility, isAnonymous: rule.isAnonymous, latitude: point.latitude, longitude: point.longitude });
    }
    router.back();
  }

  async function searchAddress() {
    if (address.trim().length === 0) {
      return;
    }
    const places = await Location.geocodeAsync(address.trim());
    const place = places[0];
    if (!place) {
      notify.warning('No encontramos esa dirección.');
      return;
    }
    setPoint({ latitude: place.latitude, longitude: place.longitude });
  }

  const needsMedia = category?.mediaRequirement !== 'None' && category?.mediaRequirement !== undefined;
  const siblings = useMemo(() => categoriesUnder(categories, category?.parentCategoryId ?? null), [categories, category?.parentCategoryId]);

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Pressable accessibilityRole="button" accessibilityLabel="Cerrar" onPress={() => void close()}><Text style={styles.link}>Cerrar</Text></Pressable>
      <Text style={styles.title}>{category?.name ?? 'Reporte'}</Text>
      <ScrollView horizontal contentContainerStyle={styles.chips}>
        {siblings.map((item) => (
          <Pressable key={item.id} accessibilityRole="button" accessibilityState={{ selected: item.id === chosenId }} accessibilityLabel={item.name} onPress={() => setChosenId(item.id)} style={[styles.chip, item.id === chosenId && styles.chipOn]}>
            <Text style={styles.chipLabel}>{item.name}</Text>
          </Pressable>
        ))}
      </ScrollView>
      <TextInput
        value={description}
        onChangeText={setDescription}
        multiline
        maxLength={maxLength}
        placeholder="¿Qué está pasando?"
        placeholderTextColor={theme.colors.textPlaceholder}
        accessibilityLabel="Descripción"
        keyboardAppearance="dark"
        style={styles.input}
      />
      <Text style={styles.counter}>{description.trim().length}/{maxLength}</Text>
      {errors.description ? <Text style={styles.error}>{errors.description}</Text> : null}
      {needsMedia ? <Text style={styles.hint}>Esta categoría pide foto o video para poder enviarse.</Text> : null}
      <Text style={styles.hint}>Si aparece un menor de edad, su imagen es un dato personal protegido.</Text>
      <View style={styles.row}>
        <Pressable accessibilityRole="button" accessibilityLabel="Tomar foto" disabled={picking} onPress={() => void addImage('camera')} style={styles.secondary}><Text style={styles.secondaryLabel}>Tomar foto</Text></Pressable>
        <Pressable accessibilityRole="button" accessibilityLabel="Elegir foto" disabled={picking} onPress={() => void addImage('library')} style={styles.secondary}><Text style={styles.secondaryLabel}>Elegir foto</Text></Pressable>
      </View>
      <View style={styles.row}>
        <Pressable accessibilityRole="button" accessibilityLabel="Grabar video" disabled={picking} onPress={() => void addVideo('camera')} style={styles.secondary}><Text style={styles.secondaryLabel}>Grabar video</Text></Pressable>
        <Pressable accessibilityRole="button" accessibilityLabel="Elegir video" disabled={picking} onPress={() => void addVideo('library')} style={styles.secondary}><Text style={styles.secondaryLabel}>Elegir video</Text></Pressable>
      </View>
      {uploads.tasks.map((task) => (
        <View key={task.id} style={styles.upload}>
          {task.kind === 'image' ? <Image source={{ uri: task.uri }} style={styles.thumb} /> : null}
          <Text style={styles.hint}>{task.kind === 'image' ? 'Foto' : 'Video'} · {task.status === 'ready' ? 'Lista' : `${task.progress}%`}</Text>
          <View style={styles.track}><View style={[styles.trackFill, { width: `${task.progress}%` }]} /></View>
          {task.status === 'failed' || task.status === 'cancelled' ? (
            <Pressable accessibilityRole="button" accessibilityLabel="Reintentar archivo" onPress={() => uploads.retry(task.id)}><Text style={styles.link}>Reintentar</Text></Pressable>
          ) : task.status !== 'ready' ? (
            <Pressable accessibilityRole="button" accessibilityLabel="Cancelar archivo" onPress={() => uploads.cancel(task.id)}><Text style={styles.link}>Cancelar</Text></Pressable>
          ) : null}
          {task.error ? <Text style={styles.error}>{task.error}</Text> : null}
        </View>
      ))}
      {errors.media ? <Text style={styles.error}>{errors.media}</Text> : null}
      <TextInput value={address} onChangeText={setAddress} placeholder="Ajustar dirección" placeholderTextColor={theme.colors.textPlaceholder} accessibilityLabel="Buscar dirección" keyboardAppearance="dark" style={styles.address} />
      <Pressable accessibilityRole="button" accessibilityLabel="Buscar dirección" onPress={() => void searchAddress()}><Text style={styles.link}>Llevar el pin a esa dirección</Text></Pressable>
      {mapChoice.status === 'unavailable' ? (
        <Text style={styles.hint}>El mapa no está disponible en este dispositivo todavía. Puedes buscar la dirección.</Text>
      ) : (
        <MapView style={styles.map} provider={PROVIDER_DEFAULT} region={{ ...point, latitudeDelta: 0.01, longitudeDelta: 0.01 }}>
          <Marker coordinate={point} draggable accessibilityLabel="Mover el lugar del reporte" onDragEnd={(event) => setPoint(event.nativeEvent.coordinate)} />
        </MapView>
      )}
      <View style={styles.row}>
        <Choice label="Público en el mapa" selected={visibility === 'Public'} onPress={() => setVisibility('Public')} />
        <Choice label="Solo amigos" selected={visibility === 'FriendsOnly'} onPress={() => setVisibility('FriendsOnly')} />
      </View>
      <Pressable
        accessibilityRole="switch"
        accessibilityState={{ checked: rule.isAnonymous }}
        accessibilityLabel="Publicar como anónimo"
        onPress={() => {
          if (rule.explain) {
            return;
          }
          identityLocked.current = true;
          const next = !anonymous;
          setAnonymous(next);
          void api.updateMyProfile({ preferAnonymousReports: next }).catch(() => undefined);
        }}
        style={styles.switchRow}
      >
        <View style={[styles.switchKnob, rule.isAnonymous && styles.switchOn]} />
        <Text style={styles.hint}>Publicar como anónimo</Text>
      </Pressable>
      <Text style={styles.hint}>{anonymityStatus(rule.isAnonymous)}</Text>
      {rule.explain ? <Text style={styles.hint}>Como eres menor de edad, un reporte público se publica como anónimo. Si prefieres tu nombre, elige Solo amigos.</Text> : null}
      {!anonymityNoticeAccepted(me.data) ? (
        <View>
          <Text style={styles.hint}>{anonymityNotice.title}. {anonymityNotice.body}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Entendido" onPress={() => void api.acknowledgeAnonymityNotice().then(() => queryClient.invalidateQueries({ queryKey: ['me'] }))}>
            <Text style={styles.link}>Entendido</Text>
          </Pressable>
        </View>
      ) : null}
      <Pressable accessibilityRole="button" accessibilityLabel="Publicar reporte" disabled={create.isPending} onPress={() => void submit()} style={styles.send}>
        <Text style={styles.sendLabel}>{create.isPending ? 'Publicando…' : 'Publicar'}</Text>
      </Pressable>
    </ScrollView>
  );
}

async function allowCamera(): Promise<boolean> {
  const permission = await ImagePicker.requestCameraPermissionsAsync();
  if (permission.granted) {
    return true;
  }
  await alertModal({ title: feedbackMessages.cameraDeniedTitle, message: feedbackMessages.cameraDeniedBody, type: 'warning', buttonText: feedbackMessages.openSettings });
  return false;
}

async function explainPublishBlock(
  block: Exclude<ReturnType<typeof publishBlock>, { kind: 'allowed' }>,
  router: { push: (href: '/esperando-autorizacion') => void },
) {
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
    await alertModal({ title: 'Falta la autorización', message: 'Tu representante todavía no autoriza la cuenta.', type: 'warning', buttonText: 'Continuar' });
    router.push('/esperando-autorizacion');
    return;
  }

  await alertModal({ title: 'No puedes publicar', message: block.message, type: 'warning' });
}

async function handleSubmitError(
  error: unknown,
  retry: () => void,
  router: { push: (href: '/esperando-autorizacion') => void },
  setErrors: (errors: { description?: string; media?: string }) => void,
) {
  if (!(error instanceof AppError)) {
    notify.error(error, { action: { label: 'Reintentar', onPress: retry } });
    return;
  }
  if (error.apiCode === 'report.email_unconfirmed') {
    await explainPublishBlock({ kind: 'confirm-email' }, router);
    return;
  }
  if (error.apiCode === 'report.pending_guardian') {
    await explainPublishBlock({ kind: 'guardian' }, router);
    return;
  }
  if (error.apiCode === 'report.minor_public_identity') {
    await alertModal({ title: 'Reporte público', message: error.userMessage, type: 'warning' });
    return;
  }
  if (error.code === 'RATE_LIMIT' || error.apiCode === 'report.daily_limit' || error.apiCode === 'report.anonymous_daily_limit') {
    await alertModal({ title: 'Límite de hoy', message: retryAfterLabel(error.retryAfter), type: 'warning' });
    return;
  }
  if (error.code === 'VALIDATION') {
    setErrors({
      description: error.fieldErrors?.description ?? error.fieldErrors?.Description,
      media: error.fieldErrors?.mediaIds ?? error.fieldErrors?.MediaIds ?? error.userMessage,
    });
    return;
  }
  notify.error(error, { action: { label: 'Reintentar', onPress: retry } });
}

function Choice({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected }} accessibilityLabel={label} onPress={onPress} style={[styles.choice, selected && styles.chipOn]}>
      <Text style={styles.chipLabel}>{label}</Text>
    </Pressable>
  );
}

async function readDraft(): Promise<Draft | null> {
  try {
    if (!draftFile.exists) {
      return null;
    }
    return JSON.parse(await draftFile.text()) as Draft;
  } catch {
    return null;
  }
}

async function writeDraft(draft: Draft) {
  if (!draftFile.exists) {
    draftFile.create();
  }
  await draftFile.write(JSON.stringify(draft));
}

async function clearDraft() {
  if (draftFile.exists) {
    draftFile.delete();
  }
}

const styles = StyleSheet.create({
  content: { gap: 12, padding: 20, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16, minHeight: 44 },
  chips: { gap: 8 },
  chip: { minHeight: 44, paddingHorizontal: 12, borderRadius: 22, justifyContent: 'center', backgroundColor: theme.colors.surfaceElevated },
  chipOn: { backgroundColor: theme.colors.primary },
  chipLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  input: { minHeight: 120, borderRadius: theme.radius.input, borderWidth: 1, borderColor: theme.colors.border, padding: 12, color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, textAlignVertical: 'top' },
  address: { minHeight: 44, borderRadius: theme.radius.input, borderWidth: 1, borderColor: theme.colors.border, paddingHorizontal: 12, color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  counter: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12, textAlign: 'right' },
  hint: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 20, flex: 1 },
  error: { color: theme.colors.dangerSolid, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  row: { flexDirection: 'row', gap: 8 },
  secondary: { flex: 1, minHeight: 44, borderRadius: theme.radius.button, borderWidth: 1, borderColor: theme.colors.border, alignItems: 'center', justifyContent: 'center' },
  secondaryLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold },
  upload: { gap: 4 },
  thumb: { width: 72, height: 72, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  track: { height: 4, borderRadius: 2, backgroundColor: theme.colors.skeleton },
  trackFill: { height: 4, borderRadius: 2, backgroundColor: theme.colors.primary },
  map: { height: 180, borderRadius: 16 },
  choice: { flex: 1, minHeight: 44, borderRadius: theme.radius.button, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surfaceElevated },
  switchRow: { flexDirection: 'row', gap: 12, alignItems: 'center' },
  switchKnob: { width: 44, height: 28, borderRadius: 14, backgroundColor: theme.colors.border },
  switchOn: { backgroundColor: theme.colors.primary },
  send: { minHeight: 52, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
