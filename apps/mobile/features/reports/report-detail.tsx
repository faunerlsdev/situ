import {
  absoluteMediaUrl,
  AppError,
  classifyWriteError,
  defaultApiUrl,
  feedbackMessages,
  notify,
  presentAuthor,
  readApiUrl,
  relativeTime,
  sites,
  rollbackReaction,
  toggleReaction,
} from '@situ/shared';
import type { ReactionSnapshot } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useRouter } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { useEffect, useRef, useState } from 'react';
import { Image, Linking, Pressable, Share, StyleSheet, Text, View } from 'react-native';

import { blockContent } from '@/features/interaction/block-content';
import { CommentSection } from '@/features/interaction/comment-section';
import { ComplaintSheet } from '@/features/interaction/complaint-sheet';
import type { ComplaintTarget } from '@/features/interaction/complaint-sheet';
import { responseCommitment } from '@/features/interaction/support-copy';
import { PhotoViewer } from '@/features/reports/photo-viewer';
import { useReportCatalog } from '@/features/reports/use-catalog';
import { useAppConfig } from '@/features/reports/use-app-config';
import { api } from '@/lib/api';
import { supportFallbackUrl } from '@/lib/store-links';
import { theme } from '@/theme/theme';

const avatar = require('../../assets/images/brand/situ-isotipo-naranja-claro.png');
const apiBase = readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl);
const clockIcon = { ios: 'clock', android: 'schedule', web: 'schedule' } as const;
const flagIcon = { ios: 'flag', android: 'flag', web: 'flag' } as const;
const personIcon = { ios: 'person.crop.circle.badge.exclamationmark', android: 'report', web: 'report' } as const;
const blockIcon = { ios: 'hand.raised', android: 'block', web: 'block' } as const;
const shareIcon = { ios: 'square.and.arrow.up', android: 'share', web: 'share' } as const;
const appearsIcon = { ios: 'questionmark.circle', android: 'help', web: 'help' } as const;
const likeIcon = { ios: 'hand.thumbsup', android: 'thumb_up', web: 'thumb_up' } as const;
const likeOnIcon = { ios: 'hand.thumbsup.fill', android: 'thumb_up', web: 'thumb_up' } as const;

export function ReportDetail({ id, focusComments = false, openPhoto = false }: { id: string; focusComments?: boolean; openPhoto?: boolean }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const config = useAppConfig();
  const catalog = useReportCatalog();
  const query = useQuery({ queryKey: ['report', id], queryFn: () => api.report(id) });
  const unavailable = query.isError && query.error instanceof AppError && query.error.apiCode === 'report.not_available';

  useEffect(() => {
    if (unavailable) {
      router.replace('/chisme');
    }
  }, [router, unavailable]);
  const [photo, setPhoto] = useState<string | null>(null);
  const openedPhoto = useRef(false);

  useEffect(() => {
    if (!openPhoto || openedPhoto.current || !query.data) {
      return;
    }
    const image = query.data.media.find((media) => media.type !== 'Video');
    const uri = image ? absoluteMediaUrl(apiBase, image.url) : null;
    if (!uri) {
      return;
    }
    openedPhoto.current = true;
    setPhoto(uri);
  }, [openPhoto, query.data]);
  const [complaint, setComplaint] = useState<ComplaintTarget | null>(null);
  const [complaintPending, setComplaintPending] = useState(false);
  const [duplicate, setDuplicate] = useState<string | null>(null);

  if (query.isLoading) {
    return <View style={styles.skeleton} />;
  }
  if (unavailable) {
    return <View style={styles.page} />;
  }
  if (query.isError || !query.data) {
    return (
      <View style={styles.page}>
        <Text style={styles.body}>No pudimos abrir el reporte.</Text>
        <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void query.refetch()} style={styles.button}>
          <Text style={styles.buttonLabel}>Reintentar</Text>
        </Pressable>
      </View>
    );
  }

  const report = query.data.report;
  const author = presentAuthor(report.author);
  const title = catalog.data?.categories.find((category) => category.code === report.categoryCode)?.name ?? 'Reporte';
  const likes = report.reactionCounts.reduce((sum, item) => sum + Number(item.count), 0);
  const liked = report.myReaction === 'Like';

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
    if (!complaint) {
      return;
    }
    setComplaintPending(true);
    try {
      await api.submitComplaint({ targetType: complaint.type, targetId: complaint.id, reason, details: details || null });
      setComplaint(null);
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
            <View style={styles.author}>
              {author.anonymous ? (
                <Image source={avatar} style={styles.avatar} />
              ) : (
                <View style={styles.initial}>
                  <Text style={styles.initialText}>{author.name.trim().charAt(0).toUpperCase() || 'S'}</Text>
                </View>
              )}
              <View style={styles.authorCopy}>
                <Text style={styles.name}>{author.name}</Text>
                <View style={styles.timeRow}>
                  <SymbolView name={clockIcon} tintColor={theme.colors.textSecondary} size={12} />
                  <Text style={styles.time}>{relativeTime(report.createdAt)}</Text>
                  {author.anonymous ? <Text style={styles.time}>· Anónimo</Text> : null}
                  {report.status === 'Expired' ? <Text style={styles.time}>· Ya no está en el mapa</Text> : null}
                </View>
              </View>
            </View>
            <Text style={styles.title}>{title}</Text>
            {report.description ? <Text style={styles.body}>{report.description}</Text> : null}
            {query.data.media.map((media) => {
              const uri = absoluteMediaUrl(apiBase, media.url) ?? '';
              return media.type === 'Video' ? (
                <ReportVideo key={media.id} uri={uri} />
              ) : (
                <Pressable key={media.id} accessibilityRole="imagebutton" accessibilityLabel="Ver foto" onPress={() => setPhoto(uri)}>
                  <Image source={{ uri }} style={styles.photo} />
                </Pressable>
              );
            })}
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={liked ? 'Quitar me gusta' : 'Me gusta'}
              accessibilityState={{ selected: liked }}
              onPress={() => void react('Like')}
              style={styles.like}
            >
              <SymbolView name={liked ? likeOnIcon : likeIcon} tintColor={liked ? theme.colors.primary : theme.colors.textSecondary} size={16} />
              <Text style={[styles.likeLabel, liked && styles.likeOn]}>Me gusta{likes > 0 ? ` ${likes}` : ''}</Text>
            </Pressable>
            <View style={styles.actions}>
              <Pressable accessibilityRole="button" accessibilityLabel="Denunciar" onPress={() => { setDuplicate(null); setComplaint({ type: 'Report', id }); }} style={styles.iconButton}>
                <SymbolView name={flagIcon} tintColor={theme.colors.textSecondary} size={20} />
              </Pressable>
              {!report.author.isAnonymous && report.author.userId ? (
                <Pressable accessibilityRole="button" accessibilityLabel="Denunciar usuario" onPress={() => { setDuplicate(null); setComplaint({ type: 'User', id: report.author.userId ?? '' }); }} style={styles.iconButton}>
                  <SymbolView name={personIcon} tintColor={theme.colors.textSecondary} size={20} />
                </Pressable>
              ) : null}
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Bloquear"
                onPress={() => void blockContent(queryClient, { anonymous: report.author.isAnonymous, reportId: id, userId: report.author.userId })}
                style={styles.iconButton}
              >
                <SymbolView name={blockIcon} tintColor={theme.colors.textSecondary} size={20} />
              </Pressable>
              <Pressable accessibilityRole="button" accessibilityLabel="Compartir" onPress={() => void Share.share({ message: `${title} en SITU\n${sites.web}/r/${id}` })} style={styles.iconButton}>
                <SymbolView name={shareIcon} tintColor={theme.colors.textSecondary} size={20} />
              </Pressable>
              <Pressable accessibilityRole="link" accessibilityLabel="¿Apareces en este chisme?" onPress={() => void Linking.openURL(`${sites.web}/retiro-de-contenido?reporte=${id}`)} style={styles.iconButton}>
                <SymbolView name={appearsIcon} tintColor={theme.colors.textSecondary} size={20} />
              </Pressable>
            </View>
          </View>
        )}
      />
      <PhotoViewer uri={photo} onClose={() => setPhoto(null)} />
      <ComplaintSheet
        visible={complaint !== null}
        pending={complaintPending}
        duplicateMessage={duplicate}
        commitment={responseCommitment(config.data?.legal.responseCommitment)}
        supportUrl={config.data?.legal.supportUrl || supportFallbackUrl()}
        onClose={() => { setComplaint(null); setDuplicate(null); }}
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
  page: { gap: 10, paddingHorizontal: 16, paddingTop: 4, paddingBottom: 8, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 20, lineHeight: 26 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  name: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 15 },
  time: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  author: { flexDirection: 'row', gap: 10, alignItems: 'center' },
  authorCopy: { flex: 1, gap: 2 },
  timeRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 4 },
  avatar: { width: 40, height: 40, borderRadius: 20 },
  initial: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.primarySoft },
  initialText: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  photo: { width: '100%', height: 240, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  skeleton: { flex: 1, margin: 16, borderRadius: 16, backgroundColor: theme.colors.skeleton },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold },
  like: { minHeight: 40, flexDirection: 'row', alignItems: 'center', gap: 6 },
  likeLabel: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  likeOn: { color: theme.colors.primary },
  actions: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    alignItems: 'center',
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: theme.colors.border,
    marginTop: 2,
  },
  iconButton: { flex: 1, minHeight: 44, alignItems: 'center', justifyContent: 'center' },
});
