import { absoluteMediaUrl, confirm, defaultApiUrl, feedbackMessages, notify, readApiUrl, relativeTime, reportStatusLabel } from '@situ/shared';
import type { ReportCard } from '@situ/shared';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const emptyImage = require('../../assets/images/icons/situ-noticias-dark.png');
const apiBase = readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl);

export function MyReports() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<ReportCard | null>(null);
  const [description, setDescription] = useState('');
  const query = useInfiniteQuery({
    queryKey: ['reports-mine'],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) => api.myReports(pageParam, 20),
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });
  const reports = query.data?.pages.flatMap((page) => page.reports) ?? [];

  const save = useMutation({
    meta: { silentError: true },
    mutationFn: () => api.updateReport(editing?.id ?? '', description.trim()),
    onSuccess: async () => {
      setEditing(null);
      await queryClient.invalidateQueries({ queryKey: ['reports-mine'] });
      notify.success('Descripción actualizada.');
    },
    onError: (error) => notify.error(error, { action: { label: 'Reintentar', onPress: () => void save.mutate() } }),
  });

  async function remove(report: ReportCard) {
    const accepted = await confirm({ title: 'Eliminar reporte', message: 'Dejará de verse en el mapa.', type: 'danger', confirmText: 'Eliminar', cancelText: 'Cancelar' });
    if (!accepted) {
      return;
    }
    try {
      await api.deleteReport(report.id);
      await queryClient.invalidateQueries({ queryKey: ['reports-mine'] });
      await queryClient.invalidateQueries({ queryKey: ['reports-map'] });
      notify.success('Reporte retirado.');
    } catch (error) {
      notify.error(error, { action: { label: 'Reintentar', onPress: () => void remove(report) } });
    }
  }

  if (query.isLoading) {
    return (
      <View style={styles.page}>
        {[0, 1, 2].map((item) => <View key={item} style={styles.skeleton} />)}
      </View>
    );
  }

  if (query.isError) {
    return (
      <View style={styles.empty}>
        <Image source={emptyImage} style={styles.emptyImage} resizeMode="contain" />
        <Text style={styles.emptyTitle}>No pudimos cargar tus reportes.</Text>
        <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void query.refetch()} style={styles.button}>
          <Text style={styles.buttonLabel}>Reintentar</Text>
        </Pressable>
      </View>
    );
  }

  if (reports.length === 0) {
    return (
      <View style={styles.empty}>
        <Image source={emptyImage} style={styles.emptyImage} resizeMode="contain" />
        <Text style={styles.emptyTitle}>Todavía no publicas un chisme.</Text>
      </View>
    );
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      {reports.map((report) => {
        const preview = absoluteMediaUrl(apiBase, report.previewUrl);
        return (
          <View key={report.id} style={styles.card}>
            {preview ? <Image source={{ uri: preview }} style={styles.thumb} /> : <View style={styles.thumb} />}
            <View style={styles.copy}>
              <Text style={styles.status}>{reportStatusLabel(report.status)}{report.isAnonymous ? ' · Anónimo' : ''}</Text>
              {report.status === 'UnderReview' ? <Text style={styles.time}>{feedbackMessages.underReviewHelp}</Text> : null}
              <Text style={styles.body} numberOfLines={2}>{report.description}</Text>
              <Text style={styles.time}>{relativeTime(report.createdAt)}</Text>
              {report.status === 'Active' ? (
                <View style={styles.row}>
                  <Pressable accessibilityRole="button" accessibilityLabel="Editar descripción" onPress={() => { setEditing(report); setDescription(report.description); }}><Text style={styles.link}>Editar</Text></Pressable>
                  <Pressable accessibilityRole="button" accessibilityLabel="Eliminar reporte" onPress={() => void remove(report)}><Text style={styles.danger}>Eliminar</Text></Pressable>
                </View>
              ) : null}
            </View>
          </View>
        );
      })}
      {query.hasNextPage ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Cargar más" onPress={() => void query.fetchNextPage()} style={styles.button}>
          <Text style={styles.buttonLabel}>{query.isFetchingNextPage ? 'Cargando…' : 'Cargar más'}</Text>
        </Pressable>
      ) : null}
      {editing ? (
        <View style={styles.editor}>
          <TextInput value={description} onChangeText={setDescription} multiline maxLength={1000} accessibilityLabel="Nueva descripción" keyboardAppearance="dark" style={styles.input} />
          <Pressable accessibilityRole="button" accessibilityLabel="Guardar descripción" disabled={save.isPending} onPress={() => save.mutate()} style={styles.button}>
            <Text style={styles.buttonLabel}>{save.isPending ? 'Guardando…' : 'Guardar'}</Text>
          </Pressable>
        </View>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  page: { gap: 12, padding: 16, backgroundColor: theme.colors.bg, flexGrow: 1 },
  card: { flexDirection: 'row', gap: 12, padding: 12, borderRadius: 16, backgroundColor: theme.colors.surface },
  thumb: { width: 72, height: 72, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  copy: { flex: 1, gap: 4 },
  status: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 12 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 15 },
  time: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  row: { flexDirection: 'row', gap: 16 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, minHeight: 44 },
  danger: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, minHeight: 44 },
  skeleton: { height: 96, borderRadius: 16, backgroundColor: theme.colors.skeleton },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12, padding: 24, backgroundColor: theme.colors.bg },
  emptyImage: { width: 72, height: 72 },
  emptyTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, textAlign: 'center' },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 16 },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold },
  editor: { gap: 8 },
  input: { minHeight: 88, borderRadius: theme.radius.input, borderWidth: 1, borderColor: theme.colors.border, padding: 12, color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
});
