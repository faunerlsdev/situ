import { complaintReasons, feedbackMessages } from '@situ/shared';
import type { ComplaintReasonCode } from '@situ/shared';
import { SymbolView } from 'expo-symbols';
import { useEffect, useState } from 'react';
import { Linking, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { fallbackResponseCommitment } from '@/features/interaction/support-copy';
import { theme } from '@/theme/theme';

export type ComplaintTarget = {
  type: 'Report' | 'Comment' | 'User';
  id: string;
};

export type ComplaintSheetProps = {
  visible: boolean;
  pending: boolean;
  duplicateMessage: string | null;
  commitment?: string;
  supportUrl?: string | null;
  animationType?: 'none' | 'slide' | 'fade';
  onClose: () => void;
  onSubmit: (reason: ComplaintReasonCode, details: string) => void;
};

const backIcon = { ios: 'chevron.left', android: 'arrow_back', web: 'arrow_back' } as const;
const chevronIcon = { ios: 'chevron.down', android: 'expand_more', web: 'expand_more' } as const;

export function ComplaintSheet({
  visible,
  pending,
  duplicateMessage,
  commitment = fallbackResponseCommitment,
  supportUrl = null,
  animationType = 'slide',
  onClose,
  onSubmit,
}: ComplaintSheetProps) {
  const insets = useSafeAreaInsets();
  const [reason, setReason] = useState<ComplaintReasonCode>('FalseInformation');
  const [details, setDetails] = useState('');
  const [open, setOpen] = useState(false);
  const selected = complaintReasons.find((item) => item.code === reason) ?? complaintReasons[0];

  useEffect(() => {
    if (!visible) {
      setReason('FalseInformation');
      setDetails('');
      setOpen(false);
    }
  }, [visible]);

  return (
    <Modal visible={visible} animationType={animationType} onRequestClose={onClose}>
      <View style={[styles.fill, { paddingTop: insets.top, paddingBottom: insets.bottom }]}>
        <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={onClose} style={styles.back}>
          <SymbolView name={backIcon} tintColor={theme.colors.textSecondary} size={18} />
          <Text style={styles.backLabel}>Volver</Text>
        </Pressable>
        <ScrollView contentContainerStyle={styles.sheet} keyboardShouldPersistTaps="handled">
          <Text style={styles.title}>{feedbackMessages.complaintTitle}</Text>
          <Text style={styles.label}>Motivo</Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Elegir motivo"
            accessibilityState={{ expanded: open }}
            onPress={() => setOpen((value) => !value)}
            style={styles.select}
          >
            <Text style={styles.selectValue}>{selected.label}</Text>
            <SymbolView name={chevronIcon} tintColor={theme.colors.textSecondary} size={16} />
          </Pressable>
          {open ? (
            <View style={styles.options}>
              {complaintReasons.map((item) => {
                const chosen = item.code === reason;
                return (
                  <Pressable
                    key={item.code}
                    accessibilityRole="button"
                    accessibilityLabel={item.label}
                    accessibilityState={{ selected: chosen }}
                    onPress={() => {
                      setReason(item.code);
                      setOpen(false);
                    }}
                    style={styles.option}
                  >
                    <Text style={[styles.optionLabel, chosen && styles.optionOn]}>{item.label}</Text>
                  </Pressable>
                );
              })}
            </View>
          ) : null}
          <Text style={styles.label}>Detalle</Text>
          <TextInput
            value={details}
            onChangeText={setDetails}
            placeholder="Escribe el motivo, si quieres"
            placeholderTextColor={theme.colors.textPlaceholder}
            accessibilityLabel={feedbackMessages.complaintDetails}
            keyboardAppearance="dark"
            multiline
            maxLength={1000}
            style={styles.input}
          />
          <Text style={styles.commitment}>{commitment}</Text>
          {supportUrl ? (
            <Pressable accessibilityRole="link" accessibilityLabel="Contactar soporte" onPress={() => void Linking.openURL(supportUrl)}>
              <Text style={styles.support}>Contactar soporte</Text>
            </Pressable>
          ) : null}
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
        </ScrollView>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1, backgroundColor: theme.colors.bg },
  back: { minHeight: 44, flexDirection: 'row', alignItems: 'center', gap: 4, paddingHorizontal: 16 },
  backLabel: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15 },
  sheet: { gap: 8, paddingHorizontal: 16, paddingBottom: 28 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 22, marginBottom: 4 },
  label: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, marginTop: 8 },
  select: {
    minHeight: 48,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 14,
    borderRadius: 12,
    backgroundColor: theme.colors.surfaceElevated,
  },
  selectValue: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  options: { borderRadius: 12, backgroundColor: theme.colors.surface, overflow: 'hidden' },
  option: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 14 },
  optionLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 15 },
  optionOn: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
  input: {
    minHeight: 88,
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    backgroundColor: theme.colors.surfaceElevated,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 15,
  },
  commitment: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12, lineHeight: 18 },
  support: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, minHeight: 36, lineHeight: 36 },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  send: { minHeight: 44, marginTop: 8, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
