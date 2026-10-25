import { notificationMessages, notify } from '@situ/shared';
import type { InboxNotification, NotificationPage } from '@situ/shared';
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
import type { InfiniteData } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { FlatList, Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

import { notificationOpenTarget } from './logic';
import { NotificationRow } from './notification-row';

const newsIcon = require('../../assets/images/icons/situ-noticias-dark.png');

export function NotificationCenter() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const query = useInfiniteQuery({
    queryKey: ['notifications', 'list'],
    initialPageParam: null as string | null,
    queryFn: ({ pageParam }) => api.listNotifications(pageParam),
    getNextPageParam: (page) => page.nextCursor,
  });
  const items = query.data?.pages.flatMap((page) => page.notifications) ?? [];

  function open(item: InboxNotification) {
    const target = notificationOpenTarget({
      available: item.available,
      reportId: item.reportId,
      targetType: item.targetType,
    });
    if (target.kind === 'unavailable') {
      notify.warning(notificationMessages.unavailableNotice, { placement: 'top' });
      return;
    }

    if (!item.isRead) {
      void markRead(queryClient, item.id);
    }

    if (target.kind === 'report') {
      router.push({
        pathname: '/reporte/[id]',
        params: { id: target.reportId, focus: target.focusComments ? 'comentarios' : '' },
      });
    }
  }

  if (query.isLoading) {
    return (
      <View style={styles.screen}>
        <View style={styles.skeleton} />
        <View style={styles.skeleton} />
        <View style={styles.skeleton} />
      </View>
    );
  }

  if (query.isError) {
    return (
      <View style={styles.center}>
        <Text style={styles.body}>{notificationMessages.loadError}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.retry} onPress={() => void query.refetch()} style={styles.button}>
          <Text style={styles.buttonLabel}>{notificationMessages.retry}</Text>
        </Pressable>
      </View>
    );
  }

  return (
    <FlatList
      style={styles.screen}
      data={items}
      keyExtractor={(item) => item.id}
      refreshing={query.isRefetching && !query.isFetchingNextPage}
      onRefresh={() => void query.refetch()}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) {
          void query.fetchNextPage();
        }
      }}
      ListHeaderComponent={(
        <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.markAll} onPress={() => void markAll(queryClient)} style={styles.markAll}>
          <Text style={styles.markAllLabel}>{notificationMessages.markAll}</Text>
        </Pressable>
      )}
      ListEmptyComponent={(
        <View style={styles.center}>
          <Image source={newsIcon} resizeMode="contain" accessibilityLabel="" style={styles.news} />
          <Text style={styles.title}>{notificationMessages.empty}</Text>
        </View>
      )}
      renderItem={({ item }) => <NotificationRow item={item} onPress={open} />}
    />
  );
}

async function markRead(queryClient: ReturnType<typeof useQueryClient>, id: string) {
  const previousList = queryClient.getQueryData<InfiniteData<NotificationPage>>(['notifications', 'list']);
  const previousCount = queryClient.getQueryData<{ count: number }>(['notifications', 'unread']);
  queryClient.setQueryData<InfiniteData<NotificationPage>>(['notifications', 'list'], (current) => current ? {
    ...current,
    pages: current.pages.map((page) => ({
      ...page,
      notifications: page.notifications.map((item) => item.id === id ? { ...item, isRead: true } : item),
    })),
  } : current);
  queryClient.setQueryData<{ count: number }>(['notifications', 'unread'], (current) => current ? { count: Math.max(0, current.count - 1) } : current);
  try {
    await api.markNotificationRead(id);
  } catch (error) {
    queryClient.setQueryData(['notifications', 'list'], previousList);
    queryClient.setQueryData(['notifications', 'unread'], previousCount);
    notify.error(error, { placement: 'top' });
  }
}

async function markAll(queryClient: ReturnType<typeof useQueryClient>) {
  const previousList = queryClient.getQueryData<InfiniteData<NotificationPage>>(['notifications', 'list']);
  const previousCount = queryClient.getQueryData<{ count: number }>(['notifications', 'unread']);
  queryClient.setQueryData<InfiniteData<NotificationPage>>(['notifications', 'list'], (current) => current ? {
    ...current,
    pages: current.pages.map((page) => ({
      ...page,
      notifications: page.notifications.map((item) => ({ ...item, isRead: true })),
    })),
  } : current);
  queryClient.setQueryData<{ count: number }>(['notifications', 'unread'], { count: 0 });
  try {
    await api.markAllNotificationsRead();
  } catch (error) {
    queryClient.setQueryData(['notifications', 'list'], previousList);
    queryClient.setQueryData(['notifications', 'unread'], previousCount);
    notify.error(error, { placement: 'top' });
  }
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  skeleton: { height: 72, marginHorizontal: 16, marginTop: 12, borderRadius: theme.radius.card, backgroundColor: theme.colors.skeleton },
  center: { alignItems: 'center', gap: 12, padding: 32 },
  news: { width: 72, height: 72 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18, textAlign: 'center' },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, textAlign: 'center' },
  button: { minHeight: 44, minWidth: 44, justifyContent: 'center', paddingHorizontal: 16 },
  buttonLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  markAll: { minHeight: 44, alignSelf: 'flex-end', justifyContent: 'center', paddingHorizontal: 16 },
  markAllLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 15 },
});
