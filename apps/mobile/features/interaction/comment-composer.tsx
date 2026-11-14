import { anonymityStatus, commentLimit, feedbackMessages } from '@situ/shared';
import { SymbolView } from 'expo-symbols';
import { Pressable, StyleSheet, Switch, Text, TextInput, View } from 'react-native';

import { theme } from '@/theme/theme';

export type CommentComposerProps = {
  text: string;
  onChangeText: (value: string) => void;
  anonymous: boolean;
  anonymousLocked: boolean;
  onAnonymousChange: (value: boolean) => void;
  disabled: boolean;
  pending: boolean;
  notice: string | null;
  inlineError: string | null;
  countdown: string | null;
  replyingTo: string | null;
  onCancelReply: () => void;
  onSend: () => void;
};

export function CommentComposer({
  text,
  onChangeText,
  anonymous,
  anonymousLocked,
  onAnonymousChange,
  disabled,
  pending,
  notice,
  inlineError,
  countdown,
  replyingTo,
  onCancelReply,
  onSend,
}: CommentComposerProps) {
  return (
    <View style={styles.box}>
      {replyingTo ? (
        <View style={styles.replyRow}>
          <Text style={styles.hint}>Respondiendo a {replyingTo}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Cancelar respuesta" onPress={onCancelReply} style={styles.cancel}>
            <Text style={styles.link}>Cancelar</Text>
          </Pressable>
        </View>
      ) : null}
      <View style={styles.fieldRow}>
        <TextInput
          value={text}
          onChangeText={onChangeText}
          editable={!disabled && !pending}
          multiline
          maxLength={commentLimit}
          placeholder="Escribe un comentario"
          placeholderTextColor={theme.colors.textPlaceholder}
          accessibilityLabel="Comentario"
          keyboardAppearance="dark"
          style={styles.input}
        />
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={feedbackMessages.commentSend}
          disabled={disabled || pending}
          onPress={onSend}
          style={[styles.send, (disabled || pending) && styles.sendOff]}
        >
          <SymbolView name={{ ios: 'paperplane.fill', android: 'send', web: 'send' }} tintColor={theme.colors.textOnPrimary} size={16} />
        </Pressable>
      </View>
      <Text style={styles.counter}>{text.trim().length}/{commentLimit}</Text>
      <View style={styles.switchRow}>
        <Switch
          value={anonymous}
          disabled={anonymousLocked || disabled || pending}
          onValueChange={onAnonymousChange}
          accessibilityLabel={feedbackMessages.commentAnonymous}
          trackColor={{ true: theme.colors.primary, false: theme.colors.border }}
        />
        <Text style={styles.hint}>Anónimo</Text>
      </View>
      <Text style={styles.hint}>{anonymityStatus(anonymous)}</Text>
      <Text style={styles.hint}>{anonymousLocked ? feedbackMessages.commentAnonymousLocked : feedbackMessages.commentAnonymousHelp}</Text>
      {notice ? <Text style={styles.notice}>{notice}</Text> : null}
      {inlineError ? <Text style={styles.error}>{inlineError}</Text> : null}
      {countdown ? <Text style={styles.error}>{countdown}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  box: { gap: 6, paddingHorizontal: 16, paddingTop: 8, paddingBottom: 12, borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: theme.colors.border, backgroundColor: theme.colors.bg },
  replyRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  fieldRow: { flexDirection: 'row', alignItems: 'flex-end', gap: 8 },
  input: {
    flex: 1,
    minHeight: 40,
    maxHeight: 96,
    borderRadius: 20,
    paddingHorizontal: 14,
    paddingVertical: 8,
    backgroundColor: theme.colors.surfaceElevated,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 15,
  },
  counter: { color: theme.colors.textPlaceholder, fontFamily: theme.fontFamily.sans, fontSize: 11, textAlign: 'right' },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: 8, minHeight: 36 },
  hint: { flex: 1, color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12, lineHeight: 16 },
  notice: { color: theme.colors.primary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  cancel: { minHeight: 36, justifyContent: 'center' },
  send: { width: 36, height: 36, borderRadius: 18, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendOff: { opacity: 0.45 },
});
