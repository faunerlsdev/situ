import {
  alertModal,
  anonymityNotice,
  anonymityNoticeAccepted,
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
import { SymbolView } from 'expo-symbols';
import { AccessibilityInfo, Dimensions, Image, Keyboard, LayoutAnimation, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

import { useAppConfig } from '@/features/reports/use-app-config';
import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { supportFallbackUrl } from '@/lib/store-links';
import { theme } from '@/theme/theme';

import { blockContent } from './block-content';
import { CommentComposer } from './comment-composer';
import { ComplaintSheet } from './complaint-sheet';
import type { ComplaintTarget } from './complaint-sheet';
import { responseCommitment } from './support-copy';
import { ReactionBar } from './reaction-bar';

const avatar = require('../../assets/images/brand/situ-isotipo-naranja-claro.png');
const clockIcon = { ios: 'clock', android: 'schedule', web: 'schedule' } as const;
const flagIcon = { ios: 'flag', android: 'flag', web: 'flag' } as const;
const blockIcon = { ios: 'hand.raised', android: 'block', web: 'block' } as const;
const personIcon = { ios: 'person.crop.circle.badge.exclamationmark', android: 'report', web: 'report' } as const;
const editIcon = { ios: 'pencil', android: 'edit', web: 'edit' } as const;
const deleteIcon = { ios: 'trash', android: 'delete', web: 'delete' } as const;

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
  const [chosenAnonymous, setChosenAnonymous] = useState(true);
  const identityLocked = useRef(false);
  const [replyTo, setReplyTo] = useState<CommentItem | null>(null);
  const [editing, setEditing] = useState<CommentItem | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const [pending, setPending] = useState(false);
  const config = useAppConfig();
  const [complaint, setComplaint] = useState<ComplaintTarget | null>(null);
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
  const keyboardInset = useKeyboardInset();
  const comments = query.data?.pages.flatMap((page) => page.comments) ?? [];
  const scrollRef = useRef<ComponentRef<typeof ScrollView>>(null);
  const commentsY = useRef(0);

  useEffect(() => {
    if (identityLocked.current || !me.data) {
      return;
    }
    identityLocked.current = true;
    setChosenAnonymous(me.data.profile.preferAnonymousReports !== false);
  }, [me.data]);

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
    <View style={[styles.screen, keyboardInset > 0 ? { paddingBottom: keyboardInset } : null]}>
      <ScrollView
        ref={scrollRef}
        contentContainerStyle={styles.wrap}
        keyboardShouldPersistTaps="handled"
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
          onComplain={(item) => { setDuplicate(null); setComplaint({ type: 'Comment', id: item.id }); }}
          onComplainUser={(item) => {
            if (!item.author.userId) {
              return;
            }
            setDuplicate(null);
            setComplaint({ type: 'User', id: item.author.userId });
          }}
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
      {!anonymityNoticeAccepted(me.data) ? (
        <View>
          <Text style={styles.emptyTitle}>{anonymityNotice.title}</Text>
          <Text style={styles.emptyTitle}>{anonymityNotice.body}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Entendido" onPress={() => void api.acknowledgeAnonymityNotice().then(() => queryClient.invalidateQueries({ queryKey: ['me'] }))}>
            <Text style={styles.link}>Entendido</Text>
          </Pressable>
        </View>
      ) : null}
      <CommentComposer
        text={text}
        onChangeText={(value) => { setText(value); setInlineError(null); }}
        anonymous={anonymity.anonymous}
        anonymousLocked={anonymity.locked}
        onAnonymousChange={(value) => {
          identityLocked.current = true;
          setChosenAnonymous(value);
          if (!anonymity.locked) {
            void api.updateMyProfile({ preferAnonymousReports: value }).then(() => queryClient.invalidateQueries({ queryKey: ['me'] })).catch(() => undefined);
          }
        }}
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

function CommentRow({
  comment,
  nested,
  onReply,
  onEdit,
  onDelete,
  onComplain,
  onComplainUser,
  onBlock,
  onReact,
}: {
  comment: CommentItem;
  nested?: boolean;
  onReply: (item: CommentItem) => void;
  onEdit: (item: CommentItem) => void;
  onDelete: (item: CommentItem) => void;
  onComplain: (item: CommentItem) => void;
  onComplainUser: (item: CommentItem) => void;
  onBlock: (item: CommentItem) => void;
  onReact: (item: CommentItem, type: string) => void;
}) {
  const author = presentAuthor(comment.author);
  return (
    <View style={[styles.comment, nested && styles.nested]}>
      <Face name={author.name} anonymous={author.anonymous} />
      <View style={styles.copy}>
        <View style={styles.bubble}>
          <Text style={styles.name}>{author.name}</Text>
          <Text style={styles.body}>{comment.text}</Text>
        </View>
        <View style={styles.actions}>
          <SymbolView name={clockIcon} tintColor={theme.colors.textSecondary} size={12} />
          <Text style={styles.time}>{relativeTime(comment.createdAt)}</Text>
          <ReactionBar quiet counts={comment.reactionCounts} mine={comment.myReaction} onToggle={(type) => onReact(comment, type)} />
          {nested ? null : (
            <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.commentReply} onPress={() => onReply(comment)} style={styles.action}>
              <Text style={styles.meta}>{feedbackMessages.commentReply}</Text>
            </Pressable>
          )}
          {comment.isMine ? (
            <>
              <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.commentEdit} onPress={() => onEdit(comment)} style={styles.action}>
                <SymbolView name={editIcon} tintColor={theme.colors.textSecondary} size={15} />
              </Pressable>
              <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.commentDelete} onPress={() => onDelete(comment)} style={styles.action}>
                <SymbolView name={deleteIcon} tintColor={theme.colors.danger} size={15} />
              </Pressable>
            </>
          ) : (
            <>
              <Pressable accessibilityRole="button" accessibilityLabel="Denunciar comentario" onPress={() => onComplain(comment)} style={styles.action}>
                <SymbolView name={flagIcon} tintColor={theme.colors.textSecondary} size={15} />
              </Pressable>
              {!author.anonymous && comment.author.userId ? (
                <Pressable accessibilityRole="button" accessibilityLabel="Denunciar usuario" onPress={() => onComplainUser(comment)} style={styles.action}>
                  <SymbolView name={personIcon} tintColor={theme.colors.textSecondary} size={15} />
                </Pressable>
              ) : null}
              <Pressable accessibilityRole="button" accessibilityLabel="Bloquear" onPress={() => onBlock(comment)} style={styles.action}>
                <SymbolView name={blockIcon} tintColor={theme.colors.textSecondary} size={15} />
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
          onComplainUser={onComplainUser}
          onBlock={onBlock}
          onReact={onReact}
        />
      ))}
      </View>
    </View>
  );
}

function Face({ name, anonymous }: { name: string; anonymous: boolean }) {
  if (anonymous) {
    return <Image source={avatar} style={styles.avatar} accessibilityIgnoresInvertColors />;
  }
  const letter = name.trim().charAt(0).toUpperCase() || 'S';
  return (
    <View style={styles.initial}>
      <Text style={styles.initialText}>{letter}</Text>
    </View>
  );
}

function useKeyboardInset() {
  const [inset, setInset] = useState(0);

  useEffect(() => {
    const showEvent = Platform.OS === 'ios' ? 'keyboardWillChangeFrame' : 'keyboardDidShow';
    const hideEvent = Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide';
    const show = Keyboard.addListener(showEvent, (event) => {
      const next = Math.max(0, Dimensions.get('window').height - event.endCoordinates.screenY);
      if (Platform.OS === 'ios') {
        LayoutAnimation.configureNext(LayoutAnimation.create(event.duration ?? 250, LayoutAnimation.Types.keyboard, LayoutAnimation.Properties.opacity));
      }
      setInset(next);
    });
    const hide = Keyboard.addListener(hideEvent, (event) => {
      if (Platform.OS === 'ios') {
        LayoutAnimation.configureNext(LayoutAnimation.create(event.duration ?? 250, LayoutAnimation.Types.keyboard, LayoutAnimation.Properties.opacity));
      }
      setInset(0);
    });
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);

  return inset;
}

function presentWriteError(
  error: unknown,
  setInline: (value: string | null) => void,
  setRetryAt: (value: string | null) => void,
  retry: () => void,
) {
  const failure = classifyWriteError(
    error instanceof AppError
      ? {
          code: error.code,
          apiCode: error.apiCode,
          retryAfter: error.retryAfter,
          userMessage: error.fieldErrors?.text ?? error.userMessage,
        }
      : { code: 'NETWORK' },
  );
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
  heading: { marginHorizontal: 16, color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  skeleton: { height: 72, marginHorizontal: 16, borderRadius: 16, backgroundColor: theme.colors.skeleton },
  empty: { gap: 8, marginHorizontal: 16 },
  emptyTitle: { marginHorizontal: 16, color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 20 },
  comment: { flexDirection: 'row', gap: 8, marginHorizontal: 16, paddingVertical: 6 },
  nested: { marginHorizontal: 0, marginTop: 8 },
  avatar: { width: 32, height: 32, borderRadius: 16 },
  initial: { width: 32, height: 32, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.primarySoft },
  initialText: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  copy: { flex: 1, gap: 4 },
  bubble: { alignSelf: 'flex-start', maxWidth: '100%', gap: 2, paddingHorizontal: 12, paddingVertical: 8, borderRadius: 16, backgroundColor: theme.colors.surfaceElevated },
  name: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 13 },
  time: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 20 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 10 },
  action: { minHeight: 32, minWidth: 32, alignItems: 'center', justifyContent: 'center' },
  meta: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  retry: { minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  retryLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
});
