import { alertModal, AppError, notify, profileMessages } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import * as ImagePicker from 'expo-image-picker';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, Image, Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

import { canUploadAvatarPhoto } from './logic';

export function AvatarSheet({
  visible,
  isMinor,
  onClose,
}: {
  visible: boolean;
  isMinor: boolean;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const gallery = useQuery({ queryKey: ['avatar-gallery'], enabled: visible, queryFn: () => api.avatarGallery() });
  const [progress, setProgress] = useState<number | null>(null);
  const [reduceMotion, setReduceMotion] = useState(false);
  const allowPhoto = canUploadAvatarPhoto(isMinor);

  useEffect(() => {
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion);
  }, []);

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['me'] });
  }

  async function pick(id: string) {
    try {
      await api.setGalleryAvatar(id);
      await refresh();
      notify.success(profileMessages.photoUpdated);
      onClose();
    } catch (error) {
      notify.error(error, { action: { label: profileMessages.retry, onPress: () => void pick(id) } });
    }
  }

  async function clear() {
    try {
      await api.removeAvatar();
      await refresh();
      notify.success(profileMessages.photoRemoved);
      onClose();
    } catch (error) {
      notify.error(error, { action: { label: profileMessages.retry, onPress: () => void clear() } });
    }
  }

  async function upload() {
    if (!allowPhoto) {
      void alertModal({ title: profileMessages.avatar, message: profileMessages.minorPhotoBlocked, type: 'warning' });
      return;
    }
    const picked = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.85,
    });
    if (picked.canceled || !picked.assets[0]) {
      return;
    }
    const asset = picked.assets[0];
    const body = new FormData();
    body.append('file', { uri: asset.uri, name: 'avatar.jpg', type: asset.mimeType ?? 'image/jpeg' } as unknown as Blob);
    setProgress(0);
    try {
      await api.uploadAvatarPhoto(body, setProgress);
      await refresh();
      notify.success(profileMessages.photoUpdated);
      onClose();
    } catch (error) {
      if (error instanceof AppError && error.apiCode === 'avatar.minor_photo_not_allowed') {
        void alertModal({ title: profileMessages.avatar, message: profileMessages.minorPhotoBlocked, type: 'warning' });
        return;
      }
      notify.error(error, { action: { label: profileMessages.retry, onPress: () => void upload() } });
    } finally {
      setProgress(null);
    }
  }

  return (
    <Modal visible={visible} transparent animationType={reduceMotion ? 'none' : 'slide'} onRequestClose={onClose}>
      <View style={styles.fill}>
        <Pressable accessibilityLabel="Cerrar" style={styles.backdrop} onPress={onClose} />
        <View style={styles.sheet}>
          <Text style={styles.title}>{profileMessages.gallery}</Text>
          {gallery.isError ? (
            <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.retry} onPress={() => void gallery.refetch()} style={styles.row}>
              <Text style={styles.link}>{profileMessages.retry}</Text>
            </Pressable>
          ) : (
            <ScrollView contentContainerStyle={styles.grid}>
              {(gallery.data?.items ?? []).map((item) => (
                <Pressable key={item.id} accessibilityRole="button" accessibilityLabel={item.name} onPress={() => void pick(item.id)} style={styles.cell}>
                  <Image source={{ uri: item.imageUrl }} style={styles.icon} />
                </Pressable>
              ))}
            </ScrollView>
          )}
          {allowPhoto ? (
            <>
              <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.uploadPhoto} onPress={() => void upload()} style={styles.row}>
                <Text style={styles.link}>{profileMessages.uploadPhoto}</Text>
              </Pressable>
              {progress !== null ? (
                <View style={styles.track} accessibilityLabel={`Subiendo ${progress}%`}>
                  <View style={[styles.bar, { width: `${progress}%` }]} />
                </View>
              ) : null}
              <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.removePhoto} onPress={() => void clear()} style={styles.row}>
                <Text style={styles.link}>{profileMessages.removePhoto}</Text>
              </Pressable>
            </>
          ) : (
            <Text style={styles.note}>{profileMessages.minorPhoto}</Text>
          )}
          {!allowPhoto ? (
            <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.useGeneric} onPress={() => void clear()} style={styles.row}>
              <Text style={styles.link}>{profileMessages.useGeneric}</Text>
            </Pressable>
          ) : null}
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1, justifyContent: 'flex-end' },
  backdrop: { ...StyleSheet.absoluteFill, backgroundColor: 'rgba(0,0,0,0.5)' },
  sheet: { maxHeight: '70%', gap: 8, padding: 20, borderTopLeftRadius: 16, borderTopRightRadius: 16, backgroundColor: theme.colors.surface },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  cell: { width: 72, height: 72, alignItems: 'center', justifyContent: 'center' },
  icon: { width: 56, height: 56, borderRadius: 28, backgroundColor: theme.colors.bg },
  row: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  note: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  track: { height: 4, borderRadius: 2, backgroundColor: theme.colors.border, overflow: 'hidden' },
  bar: { height: 4, backgroundColor: theme.colors.primary },
});
