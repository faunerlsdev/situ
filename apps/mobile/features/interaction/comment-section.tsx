import {
  alertModal,
  AppError,
  classifyWriteError,
  commentValidation,
  composerAnonymity,
  confirm,
  feedbackMessages,
  notify,
  presentAuthor,
  publishBlock,
  rateLimitCountdown,
  relativeTime,
  rollbackReaction,
  toggleReaction,
} from '@situ/shared';
import type { CommentItem, ReactionSnapshot } from '@situ/shared';
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
import type { InfiniteData } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import type { ComponentRef, ReactNode } from 'react';
import { AccessibilityInfo, Image, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';
import { useMe } from '@/features/reports/use-me';

import { blockContent } from './block-content';
import { CommentComposer } from './comment-composer';
import { ComplaintSheet } from './complaint-sheet';
import { ReactionBar } from './reaction-bar';

const avatar = require('../../assets/images/brand/situ-isotipo-naranja-claro.png');

type Page = { comments: CommentItem[]; nextCursor: string | null };

export function CommentSection({ reportId, visibility, header, focusComments = false }: { reportId: string; visibility: string; header: ReactNode; focusComments?: boolean }) {
  const router = useRouter();
  const me = useMe();
  const queryClient = useQueryClient();
  const query = useInfiniteQuery({
    queryKey: ['comments', reportId],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) => api.comments(reportId, pageParam),
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });
  const [text, setText] = useState('');
  const [chosenAnonymous, setChosenAnonymous] = useState(false);
  const [replyTo, setReplyTo] = useState<CommentItem | null>(null);
  const [editing, setEditing] = useState<CommentItem | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const [pending, setPending] = useState(false);
  const [complaintId, setComplaintId] = useState<string | null>(null);
  const [complaintPending, setComplaintPending] = useState(false);
  const [duplicate, setDuplicate] = useState<string | null>(null);

  const anonymity = composerAnonymity({
    isMinor: me.data?.isMinor === true,
    visibility,
    chosen: chosenAnonymous,
  });
  const block = publishBlock(me.data?.canPublish === true, me.data?.publishBlockedReason);
  const notice = block.kind === 'wait' ? block.message : block.kind === 'confirm-email'
    ? 'Confirma tu correo para comentar.'
    : block.kind === 'guardian'
      ? 'Tu cuenta espera la autorización de un acudiente.'
      : null;
  const comments = query.data?.pages.flatMap((page) => page.comments) ?? [];
  const scrollRef = useRef<ComponentRef<typeof ScrollView>>(null);
  const commentsY = useRef(0);

  useEffect(() => {
    if (!focusComments) {
      return;
    }

    const timer = setTimeout(() => {
      void AccessibilityInfo.isReduceMotionEnabled().then((reduce) => {
        scrollRef.current?.scrollTo({ y: commentsY.current, animated: !reduce });
      });
    }, 300);
    return () => clearTimeout(timer);
  }, [focusComments]);
  const countdown = retryAt ? rateLimitCountdown(retryAt, now) : null;

  useEffect(() => {
    if (!retryAt) {
      return;
    }
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [retryAt]);

  async function send() {
    const validation = commentValidation(text);
    if (validation) {
      setInlineError(validation);
      return;
    }
    if (me.data && block.kind !== 'allowed') {
      if (block.kind === 'confirm-email') {
        await alertModal({ title: 'Confirma tu correo para comentar', message: 'Te enviamos un enlace cuando creaste la cuenta.', type: 'warning', buttonText: 'Reenviar correo' });
        await api.resendMyConfirmationEmail().catch((cause: unknown) => notify.error(cause));
      } else if (block.kind === 'guardian') {
        await alertModal({ title: 'Falta la autorización', message: 'Tu representante todavía no autoriza la cuenta.', type: 'warning', buttonText: 'Continuar' });
        router.push('/esperando-autorizacion');
      } else {
        await alertModal({ title: 'No puedes comentar', message: block.message, type: 'warning' });
      }
      return;
    }

    const draft = text;
    const parent = replyTo;
    const anonymous = anonymity.anonymous;
    setPending(true);
    setInlineError(null);
    setRetryAt(null);
    try {
      await api.addComment(reportId, { text: draft.trim(), isAnonymous: anonymous, parentCommentId: parent?.id ?? null });
      setText('');
      setReplyTo(null);
      await queryClient.invalidateQueries({ queryKey: ['comments', reportId] });
      await queryClient.invalidateQueries({ queryKey: ['report', reportId] });
    } catch (error) {
      presentWriteError(error, setInlineError, setRetryAt, () => void send());
    } finally {
      setPending(false);
    }
  }

  async function saveEdit() {
    if (!editing) {
      return;
    }
    const validation = commentValidation(text);
    if (validation) {
      setInlineError(validation);
      return;
    }
    const key = ['comments', reportId] as const;
    const previous = queryClient.getQueryData<InfiniteData<Page>>(key);
    const nextText = text.trim();
    queryClient.setQueryData<InfiniteData<Page>>(key, (current) => mapComments(current, (item) => item.id === editing.id ? { ...item, text: nextText } : item));
    setPending(true);
    try {
      await api.editComment(editing.id, nextText);
      setEditing(null);
      setText('');
      await queryClient.invalidateQueries({ queryKey: key });
    } catch (error) {
      queryClient.setQueryData(key, previous);
      presentWriteError(error, setInlineError, setRetryAt, () => void saveEdit());
    } finally {
      setPending(false);
    }
  }

  async function remove(comment: CommentItem) {
    const accepted = await confirm({ title: 'Eliminar comentario', message: 'Este comentario dejará de verse.', type: 'danger', confirmText: 'Eliminar', cancelText: 'Cancelar' });
    if (!accepted) {
      return;
    }
    const key = ['comments', reportId] as const;
    const previous = queryClient.getQueryData<InfiniteData<Page>>(key);
    queryClient.setQueryData<InfiniteData<Page>>(key, (current) => mapComments(current, (item) => item.id === comment.id ? null : item));
    try {
      await api.deleteComment(comment.id);
      await queryClient.invalidateQueries({ queryKey: key });
      await queryClient.invalidateQueries({ queryKey: ['report', reportId] });
    } catch (error) {
      queryClient.setQueryData(key, previous);
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void remove(comment) } });
    }
  }

  async function react(comment: CommentItem, type: string) {
    const key = ['comments', reportId] as const;
    const previous = queryClient.getQueryData<InfiniteData<Page>>(key);
    const snapshot: ReactionSnapshot = { counts: comment.reactionCounts, mine: comment.myReaction };
    const next = toggleReaction(snapshot, type);
    queryClient.setQueryData<InfiniteData<Page>>(key, (current) => mapComments(current, (item) => item.id === comment.id ? { ...item, reactionCounts: next.counts, myReaction: next.mine } : item));
    try {
      if (next.mine === null) {
        await api.clearCommentReaction(comment.id);
      } else {
        await api.setCommentReaction(comment.id, next.mine);
      }
    } catch (error) {
      queryClient.setQueryData(key, previous);
      void rollbackReaction(snapshot);
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void react(comment, type) } });
    }
  }

  async function submitComplaint(reason: Parameters<typeof api.submitComplaint>[0]['reason'], details: string) {
    if (!complaintId) {
      return;
    }
    setComplaintPending(true);
    try {
      await api.submitComplaint({ targetType: 'Comment', targetId: complaintId, reason, details: details || null });
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

  return (
    <View style={styles.screen}>
      <ScrollView
        ref={scrollRef}
        contentContainerStyle={styles.wrap}
        refreshControl={<RefreshControl refreshing={query.isRefetching && !query.isLoading} onRefresh={() => void query.refetch()} tintColor={theme.colors.primary} />}
      >
      {header}
      <Text onLayout={(event) => { commentsY.current = event.nativeEvent.layout.y; }} style={styles.heading}>Comentarios</Text>
      {query.isLoading ? <View style={styles.skeleton} /> : null}
      {query.isError ? (
        <View style={styles.empty}>
          <Text style={styles.body}>No pudimos cargar los comentarios.</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void query.refetch()} style={styles.retry}>
            <Text style={styles.retryLabel}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}
      {!query.isLoading && !query.isError && comments.length === 0 ? <Text style={styles.emptyTitle}>{feedbackMessages.commentFirst}</Text> : null}
      {comments.map((comment) => (
        <CommentRow
          key={comment.id}
          comment={comment}
          onReply={(item) => { setReplyTo(item); setEditing(null); }}
          onEdit={(item) => { setEditing(item); setText(item.text); setReplyTo(null); }}
          onDelete={(item) => void remove(item)}
          onComplain={(item) => { setDuplicate(null); setComplaintId(item.id); }}
          onBlock={(item) => void blockContent(queryClient, { anonymous: item.author.isAnonymous, reportId, commentId: item.id, userId: item.author.userId })}
          onReact={(item, type) => void react(item, type)}
        />
      ))}
      {query.hasNextPage ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Cargar más comentarios" onPress={() => void query.fetchNextPage()} style={styles.retry}>
          <Text style={styles.retryLabel}>{query.isFetchingNextPage ? 'Cargando…' : 'Cargar más'}</Text>
        </Pressable>
      ) : null}
      </ScrollView>
      <CommentComposer
        text={text}
        onChangeText={(value) => { setText(value); setInlineError(null); }}
        anonymous={anonymity.anonymous}
        anonymousLocked={anonymity.locked}
        onAnonymousChange={setChosenAnonymous}
        disabled={false}
        pending={pending}
        notice={notice}
        inlineError={inlineError}
        countdown={countdown}
        replyingTo={replyTo ? presentAuthor(replyTo.author).name : null}
        onCancelReply={() => setReplyTo(null)}
        onSend={() => void (editing ? saveEdit() : send())}
      />
      <ComplaintSheet
        visible={complaintId !== null}
        pending={complaintPending}
        duplicateMessage={duplicate}
        onClose={() => { setComplaintId(null); setDuplicate(null); }}
        onSubmit={(reason, details) => void submitComplaint(reason, details)}
      />
    </View>
  );
}

function CommentRow({
  comment,
  nested,
  onReply,
  onEdit,
  onDelete,
  onComplain,
  onBlock,
  onReact,
}: {
  comment: CommentItem;
  nested?: boolean;
  onReply: (item: CommentItem) => void;
  onEdit: (item: CommentItem) => void;
  onDelete: (item: CommentItem) => void;
  onComplain: (item: CommentItem) => void;
  onBlock: (item: CommentItem) => void;
  onReact: (item: CommentItem, type: string) => void;
}) {
  const author = presentAuthor(comment.author);
  return (
    <View style={[styles.comment, nested && styles.nested]}>
      <View style={styles.author}>
        {author.anonymous ? <Image source={avatar} style={styles.avatar} accessibilityIgnoresInvertColors /> : null}
        <View style={styles.copy}>
          <Text style={styles.name}>{author.name}</Text>
          <Text style={styles.time}>{relativeTime(comment.createdAt)}</Text>
        </View>
      </View>
      <Text style={styles.body}>{comment.text}</Text>
      <ReactionBar counts={comment.reactionCounts} mine={comment.myReaction} onToggle={(type) => onReact(comment, type)} />
      <View style={styles.actions}>
        {nested ? null : (
          <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.commentReply} onPress={() => onReply(comment)} style={styles.action}>
            <Text style={styles.link}>{feedbackMessages.commentReply}</Text>
          </Pressable>
        )}
        {comment.isMine ? (
          <>
            <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.commentEdit} onPress={() => onEdit(comment)} style={styles.action}>
              <Text style={styles.link}>{feedbackMessages.commentEdit}</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.commentDelete} onPress={() => onDelete(comment)} style={styles.action}>
              <Text style={styles.danger}>{feedbackMessages.commentDelete}</Text>
            </Pressable>
          </>
        ) : (
          <>
            <Pressable accessibilityRole="button" accessibilityLabel="Denunciar comentario" onPress={() => onComplain(comment)} style={styles.action}>
              <Text style={styles.link}>Denunciar</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel="Bloquear" onPress={() => onBlock(comment)} style={styles.action}>
              <Text style={styles.danger}>Bloquear</Text>
            </Pressable>
          </>
        )}
      </View>
      {comment.replies.map((reply) => (
        <CommentRow
          key={reply.id}
          comment={reply}
          nested
          onReply={onReply}
          onEdit={onEdit}
          onDelete={onDelete}
          onComplain={onComplain}
          onBlock={onBlock}
          onReact={onReact}
        />
      ))}
    </View>
  );
}

function presentWriteError(
  error: unknown,
  setInline: (value: string | null) => void,
  setRetryAt: (value: string | null) => void,
  retry: () => void,
) {
  const failure = classifyWriteError(error instanceof AppError ? error : { code: 'NETWORK' });
  if (failure.kind === 'inline') {
    setInline(failure.message);
    return;
  }
  if (failure.kind === 'countdown') {
    setRetryAt(error instanceof AppError && error.retryAfter ? error.retryAfter : new Date(Date.now() + 60_000).toISOString());
    return;
  }
  if (failure.kind === 'modal') {
    void alertModal({ title: 'Comentario anónimo', message: failure.message, type: 'warning' });
    return;
  }
  if (failure.kind === 'network') {
    notify.error(error, { action: { label: feedbackMessages.retry, onPress: retry } });
    return;
  }
  notify.error(error);
}

function mapComments(data: InfiniteData<Page> | undefined, change: (item: CommentItem) => CommentItem | null): InfiniteData<Page> | undefined {
  if (!data) {
    return data;
  }
  return {
    ...data,
    pages: data.pages.map((page) => ({
      ...page,
      comments: page.comments.flatMap((item) => {
        const next = change(item);
        if (next === null) {
          return [];
        }
        return [{ ...next, replies: next.replies.flatMap((reply) => {
          const updated = change(reply);
          return updated === null ? [] : [updated];
        }) }];
      }),
    })),
  };
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  wrap: { gap: 12, paddingBottom: 16 },
  heading: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18 },
  skeleton: { height: 88, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  empty: { gap: 8 },
  emptyTitle: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  comment: { gap: 6, paddingVertical: 8 },
  nested: { marginLeft: 16 },
  author: { flexDirection: 'row', gap: 8, alignItems: 'center' },
  avatar: { width: 28, height: 28 },
  copy: { gap: 2 },
  name: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  time: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  action: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  danger: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  retry: { minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  retryLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
});
