import {
  absoluteMediaUrl,
  AppError,
  classifyWriteError,
  defaultApiUrl,
  feedbackMessages,
  notificationMessages,
  notify,
  presentAuthor,
  readApiUrl,
  relativeTime,
  rollbackReaction,
  toggleReaction,
} from '@situ/shared';
import type { ReactionSnapshot } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useState } from 'react';
import { Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { blockContent } from '@/features/interaction/block-content';
import { CommentSection } from '@/features/interaction/comment-section';
import { ComplaintSheet } from '@/features/interaction/complaint-sheet';
import { ReactionBar } from '@/features/interaction/reaction-bar';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const avatar = require('../../assets/images/brand/situ-isotipo-naranja-claro.png');
const apiBase = readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl);

export function ReportDetail({ id, focusComments = false }: { id: string; focusComments?: boolean }) {
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: ['report', id], queryFn: () => api.report(id) });
  const [complaintOpen, setComplaintOpen] = useState(false);
  const [complaintPending, setComplaintPending] = useState(false);
  const [duplicate, setDuplicate] = useState<string | null>(null);

  if (query.isLoading) {
    return <View style={styles.skeleton} />;
  }
  if (query.isError || !query.data) {
    const unavailable = query.error instanceof AppError && query.error.apiCode === 'report.not_available';
    return (
      <View style={styles.page}>
        <Text style={styles.body}>{unavailable ? notificationMessages.unavailableNotice : 'No pudimos abrir el reporte.'}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void query.refetch()} style={styles.button}>
          <Text style={styles.buttonLabel}>Reintentar</Text>
        </Pressable>
      </View>
    );
  }

  const report = query.data.report;
  const author = presentAuthor(report.author);

  async function react(type: string) {
    const snapshot: ReactionSnapshot = { counts: report.reactionCounts.map((item) => ({ type: item.type, count: Number(item.count) })), mine: report.myReaction };
    const next = toggleReaction(snapshot, type);
    queryClient.setQueryData<typeof query.data>(['report', id], (current) => current ? {
      ...current,
      report: { ...current.report, reactionCounts: next.counts, myReaction: next.mine },
    } : current);
    try {
      if (next.mine === null) {
        await api.clearReportReaction(id);
      } else {
        await api.setReportReaction(id, next.mine);
      }
    } catch (error) {
      queryClient.setQueryData(['report', id], query.data);
      void rollbackReaction(snapshot);
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void react(type) } });
    }
  }

  async function submitComplaint(reason: Parameters<typeof api.submitComplaint>[0]['reason'], details: string) {
    setComplaintPending(true);
    try {
      await api.submitComplaint({ targetType: 'Report', targetId: id, reason, details: details || null });
      setComplaintOpen(false);
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

  return (
    <View style={styles.fill}>
      <CommentSection
        reportId={id}
        visibility={report.visibility}
        focusComments={focusComments}
        header={(
          <View style={styles.page}>
            <Text style={styles.title}>{report.categoryCode ?? 'Reporte'}</Text>
            <Text style={styles.meta}>{relativeTime(report.createdAt)}</Text>
            <View style={styles.author}>
              {author.anonymous ? <Image source={avatar} style={styles.avatar} /> : null}
              <View>
                <Text style={styles.body}>{author.name}</Text>
                {author.memberSince ? <Text style={styles.meta}>{author.memberSince}</Text> : null}
                {author.anonymous ? <Text style={styles.badge}>Anónimo</Text> : null}
              </View>
            </View>
            <Text style={styles.body}>{report.description}</Text>
            {query.data.media.map((media) => media.type === 'Video' ? (
              <ReportVideo key={media.id} uri={absoluteMediaUrl(apiBase, media.url) ?? ''} />
            ) : (
              <Image key={media.id} source={{ uri: absoluteMediaUrl(apiBase, media.url) ?? undefined }} style={styles.photo} />
            ))}
            <ReactionBar
              counts={report.reactionCounts.map((item) => ({ type: item.type, count: Number(item.count) }))}
              mine={report.myReaction}
              onToggle={(type) => void react(type)}
            />
            <View style={styles.row}>
              <Pressable accessibilityRole="button" accessibilityLabel="Denunciar" onPress={() => { setDuplicate(null); setComplaintOpen(true); }} style={styles.action}>
                <Text style={styles.link}>Denunciar</Text>
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Bloquear"
                onPress={() => void blockContent(queryClient, { anonymous: report.author.isAnonymous, reportId: id, userId: report.author.userId })}
                style={styles.action}
              >
                <Text style={styles.danger}>Bloquear</Text>
              </Pressable>
            </View>
          </View>
        )}
      />
      <ComplaintSheet
        visible={complaintOpen}
        pending={complaintPending}
        duplicateMessage={duplicate}
        onClose={() => { setComplaintOpen(false); setDuplicate(null); }}
        onSubmit={(reason, details) => void submitComplaint(reason, details)}
      />
    </View>
  );
}

function ReportVideo({ uri }: { uri: string }) {
  const player = useVideoPlayer(uri, (instance) => {
    instance.loop = false;
  });
  return <VideoView player={player} style={styles.photo} nativeControls />;
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  page: { gap: 12, paddingHorizontal: 20, paddingTop: 8, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  meta: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  badge: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 12 },
  author: { flexDirection: 'row', gap: 8, alignItems: 'center' },
  avatar: { width: 36, height: 36 },
  photo: { width: '100%', height: 220, borderRadius: 16, backgroundColor: theme.colors.skeleton },
  skeleton: { flex: 1, margin: 20, borderRadius: 16, backgroundColor: theme.colors.skeleton },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold },
  row: { flexDirection: 'row', gap: 16 },
  action: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
  danger: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold },
});
