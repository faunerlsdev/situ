import { commentLimit, feedbackMessages } from '@situ/shared';
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
      <TextInput
        value={text}
        onChangeText={onChangeText}
        editable={!disabled && !pending}
        multiline
        maxLength={commentLimit}
        placeholder="Escribe un comentario"
        placeholderTextColor={theme.colors.textSecondary}
        accessibilityLabel="Comentario"
        keyboardAppearance="dark"
        style={styles.input}
      />
      <Text style={styles.counter}>{text.trim().length}/{commentLimit}</Text>
      <View style={styles.switchRow}>
        <Switch
          value={anonymous}
          disabled={anonymousLocked || disabled || pending}
          onValueChange={onAnonymousChange}
          accessibilityLabel={feedbackMessages.commentAnonymous}
          trackColor={{ true: theme.colors.primary, false: theme.colors.border }}
        />
        <Text style={styles.hint}>{feedbackMessages.commentAnonymous}</Text>
      </View>
      <Text style={styles.hint}>{anonymousLocked ? feedbackMessages.commentAnonymousLocked : feedbackMessages.commentAnonymousHelp}</Text>
      {notice ? <Text style={styles.notice}>{notice}</Text> : null}
      {inlineError ? <Text style={styles.error}>{inlineError}</Text> : null}
      {countdown ? <Text style={styles.error}>{countdown}</Text> : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={feedbackMessages.commentSend}
        disabled={disabled || pending}
        onPress={onSend}
        style={[styles.send, (disabled || pending) && styles.sendOff]}
      >
        <Text style={styles.sendLabel}>{pending ? feedbackMessages.commentSending : feedbackMessages.commentSend}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  box: { gap: 8, padding: 16, borderTopWidth: 1, borderTopColor: theme.colors.border, backgroundColor: theme.colors.bg },
  replyRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  input: {
    minHeight: 72,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    padding: 12,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  counter: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12, textAlign: 'right' },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: 8, minHeight: 44 },
  hint: { flex: 1, color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 18 },
  notice: { color: theme.colors.primary, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
  cancel: { minHeight: 44, justifyContent: 'center' },
  send: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendOff: { opacity: 0.5 },
  sendLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
