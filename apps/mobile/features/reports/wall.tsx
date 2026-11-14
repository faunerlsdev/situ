import {
  absoluteMediaUrl,
  defaultApiUrl,
  notify,
  readApiUrl,
  relativeTime,
  rollbackReaction,
  sites,
  toggleReaction,
} from '@situ/shared';
import type { MyReportsResponse, ReactionSnapshot } from '@situ/shared';
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
import type { InfiniteData } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { Image, Pressable, ScrollView, Share, StyleSheet, Text, View } from 'react-native';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

import { useReportCatalog } from './use-catalog';

const emptyImage = require('../../assets/images/icons/situ-noticias-dark.png');
const apiBase = readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl);
const clockIcon = { ios: 'clock', android: 'schedule', web: 'schedule' } as const;
const likeIcon = { ios: 'hand.thumbsup', android: 'thumb_up', web: 'thumb_up' } as const;
const likeOnIcon = { ios: 'hand.thumbsup.fill', android: 'thumb_up', web: 'thumb_up' } as const;
const commentIcon = { ios: 'bubble.left', android: 'chat', web: 'chat' } as const;
const shareIcon = { ios: 'square.and.arrow.up', android: 'share', web: 'share' } as const;

export function ReportWall() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const catalog = useReportCatalog();
  const query = useInfiniteQuery({
    queryKey: ['reports-feed'],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) => api.reportFeed(pageParam, 20),
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });
  const reports = query.data?.pages.flatMap((page) => page.reports) ?? [];

  function titleOf(code: string | null): string {
    return catalog.data?.categories.find((category) => category.code === code)?.name ?? 'Reporte';
  }

  async function like(report: (typeof reports)[number]) {
    const key = ['reports-feed'] as const;
    const previous = queryClient.getQueryData<InfiniteData<MyReportsResponse>>(key);
    const snapshot: ReactionSnapshot = {
      counts: report.reactionCounts.map((item) => ({ type: item.type, count: Number(item.count) })),
      mine: report.myReaction,
    };
    const next = toggleReaction(snapshot, report.myReaction ?? 'Like');
    queryClient.setQueryData<InfiniteData<MyReportsResponse>>(key, (current) => current ? {
      ...current,
      pages: current.pages.map((page) => ({
        ...page,
        reports: page.reports.map((item) => item.id === report.id ? { ...item, reactionCounts: next.counts, myReaction: next.mine } : item),
      })),
    } : current);
    try {
      if (next.mine === null) {
        await api.clearReportReaction(report.id);
      } else {
        await api.setReportReaction(report.id, next.mine);
      }
    } catch (error) {
      queryClient.setQueryData(key, previous);
      void rollbackReaction(snapshot);
      notify.error(error);
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
        <Text style={styles.emptyTitle}>No pudimos cargar el muro.</Text>
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
        <Text style={styles.emptyTitle}>Todavía no hay chismes en el muro.</Text>
      </View>
    );
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      {reports.map((report) => {
        const title = titleOf(report.categoryCode);
        const preview = absoluteMediaUrl(apiBase, report.previewUrl);
        const liked = report.myReaction === 'Like';
        const likes = report.reactionCounts.reduce((sum, item) => sum + Number(item.count), 0);
        return (
          <View key={report.id} style={styles.card}>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={title}
              onPress={() => router.push({ pathname: '/reporte/[id]', params: { id: report.id } })}
              style={styles.copy}
            >
              <Text style={styles.title}>{title}</Text>
              <View style={styles.timeRow}>
                <SymbolView name={clockIcon} tintColor={theme.colors.textSecondary} size={12} />
                <Text style={styles.time}>{relativeTime(report.createdAt)}</Text>
                {report.status === 'Expired' ? <Text style={styles.time}>· Ya no está en el mapa</Text> : null}
              </View>
              {report.description ? <Text style={styles.body}>{report.description}</Text> : null}
            </Pressable>
            {preview ? (
              <Pressable
                accessibilityRole="imagebutton"
                accessibilityLabel="Ver foto"
                onPress={() => router.push({ pathname: '/reporte/[id]', params: { id: report.id, foto: '1' } })}
              >
                <Image source={{ uri: preview }} style={styles.photo} />
              </Pressable>
            ) : null}
            <View style={styles.actions}>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={liked ? 'Quitar me gusta' : 'Me gusta'}
                accessibilityState={{ selected: liked }}
                onPress={() => void like(report)}
                style={styles.action}
              >
                <SymbolView name={liked ? likeOnIcon : likeIcon} tintColor={liked ? theme.colors.primary : theme.colors.textSecondary} size={16} />
                <Text style={[styles.actionLabel, liked && styles.actionOn]}>Me gusta{likes > 0 ? ` ${likes}` : ''}</Text>
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Comentar"
                onPress={() => router.push({ pathname: '/reporte/[id]', params: { id: report.id, focus: 'comentarios' } })}
                style={styles.action}
              >
                <SymbolView name={commentIcon} tintColor={theme.colors.textSecondary} size={16} />
                <Text style={styles.actionLabel}>Comentar</Text>
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Compartir"
                onPress={() => void Share.share({ message: `${title} en SITU\n${sites.web}/r/${report.id}` })}
                style={styles.action}
              >
                <SymbolView name={shareIcon} tintColor={theme.colors.textSecondary} size={16} />
                <Text style={styles.actionLabel}>Compartir</Text>
              </Pressable>
            </View>
          </View>
        );
      })}
      {query.hasNextPage ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Cargar más" onPress={() => void query.fetchNextPage()} style={styles.button}>
          <Text style={styles.buttonLabel}>{query.isFetchingNextPage ? 'Cargando…' : 'Cargar más'}</Text>
        </Pressable>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  page: { gap: 10, paddingBottom: 24 },
  card: { backgroundColor: theme.colors.surface },
  copy: { gap: 6, paddingHorizontal: 16, paddingTop: 14 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18, lineHeight: 24 },
  timeRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 4 },
  time: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 21 },
  photo: { width: '100%', height: 240, marginTop: 10, backgroundColor: theme.colors.skeleton },
  actions: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 8,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: theme.colors.border,
  },
  action: { flex: 1, minHeight: 40, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6 },
  actionLabel: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  actionOn: { color: theme.colors.primary },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12, padding: 24 },
  emptyImage: { width: 96, height: 68 },
  emptyTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, textAlign: 'center' },
  button: { minHeight: 44, marginHorizontal: 16, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 16 },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  skeleton: { height: 180, marginHorizontal: 16, borderRadius: 12, backgroundColor: theme.colors.skeleton },
});
