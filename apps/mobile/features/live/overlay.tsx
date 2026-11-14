import { SymbolView } from 'expo-symbols';
import { useEffect, useRef, useState } from 'react';
import { Animated, Dimensions, FlatList, Keyboard, LayoutAnimation, Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

const personIcon = { ios: 'person.2.fill', android: 'group', web: 'group' } as const;
const likeIcon = { ios: 'hand.thumbsup', android: 'thumb_up', web: 'thumb_up' } as const;
const likeOnIcon = { ios: 'hand.thumbsup.fill', android: 'thumb_up', web: 'thumb_up' } as const;
const shareIcon = { ios: 'square.and.arrow.up', android: 'share', web: 'share' } as const;
const flagIcon = { ios: 'flag', android: 'flag', web: 'flag' } as const;
const backIcon = { ios: 'chevron.left', android: 'arrow_back', web: 'arrow_back' } as const;

export type LiveComment = {
  id: string;
  text: string;
  author: { displayName: string };
};

export function liveLikeCount(counts: { type: string; count: number | string }[] | undefined): number {
  return (counts ?? []).reduce((sum, item) => item.type === 'Like' ? sum + Number(item.count) : sum, 0);
}

export function viewerLabel(count: number): string {
  return count === 1 ? '1 persona viendo' : `${count} personas viendo`;
}

export function useKeyboardInset(): number {
  const [inset, setInset] = useState(0);
  useEffect(() => {
    const showName = Platform.OS === 'ios' ? 'keyboardWillShow' : 'keyboardDidShow';
    const hideName = Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide';
    const show = Keyboard.addListener(showName, (event) => {
      const overlap = Platform.OS === 'ios'
        ? event.endCoordinates.height
        : Dimensions.get('window').height - event.endCoordinates.screenY;
      layoutWithKeyboard(event.duration);
      setInset(overlap > 0 ? overlap : 0);
    });
    const hide = Keyboard.addListener(hideName, (event) => {
      layoutWithKeyboard(event.duration);
      setInset(0);
    });
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);
  return inset;
}

function layoutWithKeyboard(duration: number | undefined) {
  const next = duration && duration > 10 ? duration : 250;
  LayoutAnimation.configureNext({
    duration: next,
    update: { duration: next, type: LayoutAnimation.Types.keyboard },
  });
}

export function LiveTopBar({
  title,
  viewers,
  onBack,
  status,
  onAir,
}: {
  title: string;
  viewers: number | null;
  onBack?: () => void;
  status?: string | null;
  onAir?: boolean;
}) {
  const shown = title.trim();
  return (
    <View style={styles.top}>
      {onBack ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={onBack} style={styles.back}>
          <SymbolView name={backIcon} tintColor={theme.colors.textPrimary} size={18} />
        </Pressable>
      ) : null}
      {status ? (
        <View style={styles.badge}>
          <View style={[styles.dot, onAir ? styles.dotOn : null]} />
          <Text style={styles.badgeText}>{status}</Text>
        </View>
      ) : null}
      <View style={styles.topCopy}>
        {shown ? <Text style={styles.title} numberOfLines={1}>{shown}</Text> : null}
        {viewers !== null ? (
          <View style={styles.viewers}>
            <SymbolView name={personIcon} tintColor={theme.colors.textPrimary} size={14} />
            <Text style={styles.viewerCount} accessibilityLabel={viewerLabel(viewers)}>{viewers}</Text>
          </View>
        ) : null}
      </View>
    </View>
  );
}

export function LiveCommentList({ comments }: { comments: LiveComment[] }) {
  return (
    <FlatList
      data={comments}
      inverted
      keyExtractor={(item) => item.id}
      style={styles.chat}
      contentContainerStyle={styles.chatContent}
      showsVerticalScrollIndicator={false}
      keyboardShouldPersistTaps="handled"
      renderItem={({ item }) => (
        <Text style={styles.chatLine}>
          <Text style={styles.chatName}>{item.author.displayName}</Text>
          {`  ${item.text}`}
        </Text>
      )}
    />
  );
}

export function LiveActionRow({
  liked,
  likes,
  onLike,
  onShare,
  onReport,
}: {
  liked: boolean;
  likes: number;
  onLike: () => void;
  onShare: () => void;
  onReport: () => void;
}) {
  return (
    <View style={styles.actions}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={liked ? 'Quitar me gusta' : 'Me gusta'}
        accessibilityState={{ selected: liked }}
        onPress={onLike}
        style={styles.action}
      >
        <SymbolView name={liked ? likeOnIcon : likeIcon} tintColor={liked ? theme.colors.primary : theme.colors.textPrimary} size={18} />
        {likes > 0 ? <Text style={[styles.actionCount, liked && styles.actionOn]}>{likes}</Text> : null}
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Compartir transmisión" onPress={onShare} style={styles.action}>
        <SymbolView name={shareIcon} tintColor={theme.colors.textPrimary} size={18} />
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Denunciar" onPress={onReport} style={styles.action}>
        <SymbolView name={flagIcon} tintColor={theme.colors.textPrimary} size={18} />
      </Pressable>
    </View>
  );
}

export function LiveReactionMark({ count }: { count: number }) {
  const scale = useRef(new Animated.Value(1)).current;
  const seen = useRef(count);
  useEffect(() => {
    if (count <= seen.current) {
      seen.current = count;
      return;
    }
    seen.current = count;
    scale.setValue(1);
    Animated.sequence([
      Animated.timing(scale, { toValue: 1.35, duration: 140, useNativeDriver: true }),
      Animated.timing(scale, { toValue: 1, duration: 180, useNativeDriver: true }),
    ]).start();
  }, [count, scale]);
  if (count <= 0) {
    return null;
  }
  return (
    <Animated.View style={[styles.reaction, { transform: [{ scale }] }]} accessibilityLabel={`${count} me gusta`}>
      <SymbolView name={likeOnIcon} tintColor={theme.colors.primary} size={16} />
      <Text style={styles.actionOn}>{count}</Text>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  top: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12 },
  back: { width: 36, height: 36, borderRadius: 18, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(20,20,20,0.45)' },
  topCopy: { flex: 1, gap: 2 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16, lineHeight: 20 },
  viewers: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  viewerCount: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 16 },
  badge: { flexDirection: 'row', alignItems: 'center', gap: 6, height: 28, paddingHorizontal: 10, borderRadius: 14, backgroundColor: 'rgba(20,20,20,0.45)' },
  dot: { width: 6, height: 6, borderRadius: 3, backgroundColor: theme.colors.textSecondary },
  dotOn: { backgroundColor: theme.colors.live },
  badgeText: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 16 },
  chat: { maxHeight: 220, flexShrink: 1 },
  chatContent: { gap: 8, paddingHorizontal: 12 },
  chatLine: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 18, backgroundColor: 'rgba(20,20,20,0.45)', borderRadius: 14, overflow: 'hidden', paddingHorizontal: 10, paddingVertical: 6, alignSelf: 'flex-start' },
  chatName: { fontFamily: theme.fontFamily.sansBold },
  actions: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  action: { minWidth: 36, minHeight: 36, alignItems: 'center', justifyContent: 'center', gap: 2 },
  actionCount: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 11, lineHeight: 14 },
  actionOn: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 12, lineHeight: 14 },
  reaction: { flexDirection: 'row', alignItems: 'center', gap: 4, alignSelf: 'flex-end', marginRight: 16, paddingHorizontal: 10, paddingVertical: 6, borderRadius: 16, backgroundColor: 'rgba(20,20,20,0.45)' },
});
