import { confirm, feedbackMessages, messageFromUnknown, notify } from '@situ/shared';
import type { ComplaintReasonCode } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ComplaintSheet } from '@/features/interaction/complaint-sheet';
import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { searchParam } from '@/lib/device';
import { theme } from '@/theme/theme';

const months = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

export default function PublicProfileScreen() {
  const params = useLocalSearchParams<{ userName?: string | string[] }>();
  const userName = searchParam(params.userName);
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const queryClient = useQueryClient();
  const [complaint, setComplaint] = useState(false);
  const [pending, setPending] = useState(false);
  const profile = useQuery({
    queryKey: ['public-profile', userName],
    enabled: userName.length > 0,
    queryFn: () => api.publicProfile(userName),
  });
  const mine = me.data?.userName === userName;
  const data = profile.data;

  async function requestFriend() {
    if (!data) {
      return;
    }
    setPending(true);
    try {
      const result = await api.sendFriendRequest(data.userId);
      notify.success(result.status === 'incoming' ? 'Ya tienes una solicitud de esta persona en Amigos.' : 'Si la persona puede recibirla, la verá en sus solicitudes.');
      await queryClient.invalidateQueries({ queryKey: ['friend-requests'] });
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  async function remove() {
    if (!data) {
      return;
    }
    const accepted = await confirm({
      title: 'Eliminar amigo',
      message: 'Dejan de ser amigos y se corta la ubicación que compartían.',
      type: 'danger',
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
    });
    if (!accepted) {
      return;
    }
    setPending(true);
    try {
      await api.removeFriend(data.userId);
      await queryClient.invalidateQueries({ queryKey: ['friends'] });
      await queryClient.invalidateQueries({ queryKey: ['public-profile', userName] });
      notify.success('Ya no son amigos.');
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  async function block() {
    if (!data) {
      return;
    }
    const accepted = await confirm({
      title: feedbackMessages.blockUserTitle,
      message: feedbackMessages.blockUserBody,
      type: 'danger',
      confirmText: 'Bloquear',
      cancelText: 'Cancelar',
    });
    if (!accepted) {
      return;
    }
    setPending(true);
    try {
      await api.blockUser(data.userId);
      await queryClient.invalidateQueries({ queryKey: ['friends'] });
      await queryClient.invalidateQueries({ queryKey: ['reports-map'] });
      notify.success('Bloqueaste a esta persona.');
      router.back();
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  async function submitComplaint(reason: ComplaintReasonCode, details: string) {
    if (!data) {
      return;
    }
    setPending(true);
    try {
      await api.submitComplaint({ targetType: 'User', targetId: data.userId, reason, details });
      setComplaint(false);
      notify.success('Recibimos la denuncia. La revisaremos.');
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
      {profile.isLoading ? <View style={styles.skeleton} /> : null}
      {profile.isError ? (
        <View style={styles.card}>
          <Text style={styles.body}>{messageFromUnknown(profile.error)}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void profile.refetch()} style={styles.back}>
            <Text style={styles.link}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}
      {data ? (
        <View style={styles.card}>
          {data.avatarUrl ? <Image source={{ uri: data.avatarUrl }} style={styles.avatar} accessibilityIgnoresInvertColors /> : <View style={styles.avatar} />}
          <Text style={styles.title}>{data.displayName}</Text>
          <Text style={styles.body}>@{data.userName}</Text>
          <Text style={styles.body}>{data.bio ?? 'Sin biografía'}</Text>
          <Text style={styles.body}>Miembro desde {months[(data.memberSinceMonth || 1) - 1]} de {data.memberSinceYear}</Text>
          {data.isFriend ? <Text style={styles.body}>{data.sharesLocation ? 'Comparte ubicación contigo' : 'No comparte ubicación'}</Text> : null}
          {mine ? (
            <Pressable accessibilityRole="button" accessibilityLabel="Visibilidad de mi perfil" onPress={() => router.push('/visibilidad')} style={styles.back}>
              <Text style={styles.link}>Visibilidad de mi perfil</Text>
            </Pressable>
          ) : (
            <View style={styles.actions}>
              {data.isFriend ? (
                <Pressable accessibilityRole="button" accessibilityLabel="Eliminar amigo" disabled={pending} onPress={() => void remove()} style={styles.back}>
                  <Text style={styles.danger}>Eliminar amigo</Text>
                </Pressable>
              ) : (
                <Pressable accessibilityRole="button" accessibilityLabel="Enviar solicitud" disabled={pending} onPress={() => void requestFriend()} style={styles.back}>
                  <Text style={styles.link}>Enviar solicitud</Text>
                </Pressable>
              )}
              <Pressable accessibilityRole="button" accessibilityLabel="Denunciar perfil" onPress={() => setComplaint(true)} style={styles.back}>
                <Text style={styles.link}>Denunciar</Text>
              </Pressable>
              <Pressable accessibilityRole="button" accessibilityLabel="Bloquear" disabled={pending} onPress={() => void block()} style={styles.back}>
                <Text style={styles.danger}>Bloquear</Text>
              </Pressable>
            </View>
          )}
          <Text style={styles.heading}>Reportes con nombre</Text>
          {data.reports.length === 0 ? <Text style={styles.body}>No hay reportes con nombre para mostrar.</Text> : null}
          {data.reports.map((report) => (
            <Pressable key={report.id} accessibilityRole="button" accessibilityLabel="Abrir reporte" onPress={() => router.push(`/reporte/${report.id}`)} style={styles.report}>
              <Text style={styles.body}>{report.excerpt}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
      <ComplaintSheet visible={complaint} pending={pending} duplicateMessage={null} onClose={() => setComplaint(false)} onSubmit={(reason, details) => void submitComplaint(reason, details)} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 20 },
  back: { minHeight: 44, justifyContent: 'center' },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  heading: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18, marginTop: 8 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  card: { gap: 8, padding: 16, borderRadius: 12, backgroundColor: theme.colors.surface },
  avatar: { width: 72, height: 72, borderRadius: 36, backgroundColor: theme.colors.skeleton },
  actions: { gap: 4 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  danger: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  report: { paddingVertical: 8 },
  skeleton: { height: 160, borderRadius: 12, backgroundColor: theme.colors.skeleton },
});
