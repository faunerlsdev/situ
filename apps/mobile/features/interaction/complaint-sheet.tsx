import { complaintReasons, feedbackMessages } from '@situ/shared';
import type { ComplaintReasonCode } from '@situ/shared';
import { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { theme } from '@/theme/theme';

export type ComplaintSheetProps = {
  visible: boolean;
  pending: boolean;
  duplicateMessage: string | null;
  animationType?: 'none' | 'slide' | 'fade';
  onClose: () => void;
  onSubmit: (reason: ComplaintReasonCode, details: string) => void;
};

export function ComplaintSheet({ visible, pending, duplicateMessage, animationType = 'slide', onClose, onSubmit }: ComplaintSheetProps) {
  const [reason, setReason] = useState<ComplaintReasonCode>('FalseInformation');
  const [details, setDetails] = useState('');

  return (
    <Modal visible={visible} transparent animationType={animationType} onRequestClose={onClose}>
      <View style={styles.fill}>
        <Pressable accessibilityLabel="Cerrar denuncia" style={styles.backdrop} onPress={onClose} />
        <View style={styles.sheet}>
          <Text style={styles.title}>{feedbackMessages.complaintTitle}</Text>
          {complaintReasons.map((item) => {
            const selected = item.code === reason;
            return (
              <Pressable
                key={item.code}
                accessibilityRole="button"
                accessibilityLabel={item.label}
                accessibilityState={{ selected }}
                onPress={() => setReason(item.code)}
                style={styles.reason}
              >
                <Text style={[styles.reasonLabel, selected && styles.reasonOn]}>{item.label}</Text>
              </Pressable>
            );
          })}
          <TextInput
            value={details}
            onChangeText={setDetails}
            placeholder={feedbackMessages.complaintDetails}
            placeholderTextColor={theme.colors.textSecondary}
            accessibilityLabel={feedbackMessages.complaintDetails}
            keyboardAppearance="dark"
            multiline
            maxLength={1000}
            style={styles.input}
          />
          {duplicateMessage ? <Text style={styles.error}>{duplicateMessage}</Text> : null}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={feedbackMessages.complaintSend}
            disabled={pending || duplicateMessage !== null}
            onPress={() => {
              if (pending || duplicateMessage) {
                return;
              }
              onSubmit(reason, details.trim());
            }}
            style={styles.send}
          >
            <Text style={styles.sendLabel}>{pending ? 'Enviando…' : feedbackMessages.complaintSend}</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1, justifyContent: 'flex-end' },
  backdrop: { ...StyleSheet.absoluteFill, backgroundColor: 'rgba(0,0,0,0.55)' },
  sheet: { gap: 4, padding: 16, paddingBottom: 28, borderTopLeftRadius: 16, borderTopRightRadius: 16, backgroundColor: theme.colors.surface },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 20, marginBottom: 8 },
  reason: { minHeight: 44, justifyContent: 'center' },
  reasonLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  reasonOn: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
  input: {
    minHeight: 72,
    marginTop: 8,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    padding: 12,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  send: { minHeight: 44, marginTop: 8, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
