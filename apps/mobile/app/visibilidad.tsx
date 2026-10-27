import { notify } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function VisibilityScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const queryClient = useQueryClient();
  const [accepted, setAccepted] = useState(false);
  const [pending, setPending] = useState(false);
  const visibility = useQuery({ queryKey: ['profile-visibility'], queryFn: () => api.profileVisibility() });
  const data = visibility.data;
  const minor = me.data?.isMinor === true;

  async function save(next: { public: boolean; showBio?: boolean; showReports?: boolean }) {
    if (!data) {
      return;
    }
    if (next.public && minor) {
      notify.warning('Un menor de edad tiene que mantener el perfil privado.');
      return;
    }
    if (next.public && !accepted && !data.public) {
      notify.warning('Lee el texto y marca que estás de acuerdo.');
      return;
    }
    setPending(true);
    try {
      await api.setProfileVisibility({
        public: next.public,
        consentVersion: next.public ? data.requiredConsentVersion : null,
        showBio: next.showBio ?? data.showBio,
        showReports: next.showReports ?? data.showReports,
      });
      await queryClient.invalidateQueries({ queryKey: ['profile-visibility'] });
      notify.success(next.public ? 'Tu perfil ahora es público.' : 'Tu perfil volvió a ser privado.');
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
      <Text style={styles.title}>Visibilidad de mi perfil</Text>
      {visibility.isLoading ? <View style={styles.skeleton} /> : null}
      {visibility.isError ? (
        <View style={styles.card}>
          <Text style={styles.body}>No pudimos cargar la visibilidad.</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void visibility.refetch()} style={styles.back}>
            <Text style={styles.link}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}
      {data ? (
        <>
          <Text style={styles.body}>{data.public ? 'Hoy tu perfil es público.' : 'Hoy tu perfil es privado. Solo tus amigos lo ven.'}</Text>
          {minor ? <Text style={styles.body}>Por tu edad el perfil se queda privado. No se publica tu nombre en el mapa.</Text> : null}
          {!minor && !data.public ? (
            <View style={styles.card}>
              <Text style={styles.heading}>Consentimiento</Text>
              <Text style={styles.body}>{data.consentText}</Text>
              <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: accepted }} accessibilityLabel="Acepto hacer público mi perfil" onPress={() => setAccepted((value) => !value)} style={styles.back}>
                <Text style={styles.body}>{accepted ? '☑ Acepto este texto' : '☐ Acepto este texto'}</Text>
              </Pressable>
              <Pressable accessibilityRole="button" accessibilityLabel="Hacer público mi perfil" disabled={pending} onPress={() => void save({ public: true })} style={styles.button}>
                <Text style={styles.buttonLabel}>{pending ? 'Guardando…' : 'Hacer público mi perfil'}</Text>
              </Pressable>
            </View>
          ) : null}
          {!minor && data.public ? (
            <Pressable accessibilityRole="button" accessibilityLabel="Volver a privado" disabled={pending} onPress={() => void save({ public: false })} style={styles.button}>
              <Text style={styles.buttonLabel}>Volver a privado</Text>
            </Pressable>
          ) : null}
          <Text style={styles.heading}>Qué se muestra</Text>
          <Pressable accessibilityRole="switch" accessibilityState={{ checked: data.showBio }} accessibilityLabel="Mostrar biografía" disabled={pending} onPress={() => void save({ public: data.public, showBio: !data.showBio })} style={styles.card}>
            <Text style={styles.body}>{data.showBio ? 'Biografía visible' : 'Biografía oculta'}</Text>
          </Pressable>
          <Pressable accessibilityRole="switch" accessibilityState={{ checked: data.showReports }} accessibilityLabel="Mostrar reportes con nombre" disabled={pending} onPress={() => void save({ public: data.public, showReports: !data.showReports })} style={styles.card}>
            <Text style={styles.body}>{data.showReports ? 'Reportes con nombre visibles' : 'Reportes con nombre ocultos'}</Text>
          </Pressable>
          <Text style={styles.heading}>Así te ven los demás</Text>
          {data.strangerPreview ? (
            <View style={styles.card}>
              <Text style={styles.body}>{data.strangerPreview.displayName} (@{data.strangerPreview.userName})</Text>
              <Text style={styles.body}>{data.strangerPreview.bio ?? 'Sin biografía'}</Text>
              <Text style={styles.body}>{data.strangerPreview.reports.length} reportes con nombre</Text>
            </View>
          ) : (
            <Text style={styles.body}>No apareces. Quien no es tu amigo ve lo mismo que si el perfil no existiera.</Text>
          )}
          <Text style={styles.heading}>Así te ven tus amigos</Text>
          <View style={styles.card}>
            <Text style={styles.body}>{data.friendPreview.displayName} (@{data.friendPreview.userName})</Text>
            <Text style={styles.body}>{data.friendPreview.bio ?? 'Sin biografía'}</Text>
            <Text style={styles.body}>{data.friendPreview.reports.length} reportes con nombre</Text>
          </View>
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
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  skeleton: { height: 120, borderRadius: 12, backgroundColor: theme.colors.skeleton },
});
