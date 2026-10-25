import { notify, profileMessages } from '@situ/shared';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { passwordFieldErrors, passwordStrength } from '@/features/account/logic';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function ChangePasswordScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState<{ current?: string; next?: string; confirm?: string }>({});
  const [pending, setPending] = useState(false);
  const strength = passwordStrength(next);

  async function save() {
    const nextErrors = passwordFieldErrors({ current, next, confirm });
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      return;
    }
    setPending(true);
    try {
      await api.changePassword({ currentPassword: current, newPassword: next, keepCurrentSession: true });
      notify.success(profileMessages.passwordChanged);
      router.back();
    } catch (error) {
      notify.error(error, { action: { label: profileMessages.retry, onPress: () => void save() } });
    } finally {
      setPending(false);
    }
  }

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 12, paddingBottom: insets.bottom + 24 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.back} onPress={() => router.back()} style={styles.row}>
        <Text style={styles.link}>{profileMessages.back}</Text>
      </Pressable>
      <Text style={styles.title}>{profileMessages.changePassword}</Text>
      <Text style={styles.body}>{profileMessages.otherSessions}</Text>
      <Secret label={profileMessages.currentPassword} value={current} onChangeText={setCurrent} error={errors.current} />
      <Secret label={profileMessages.newPassword} value={next} onChangeText={setNext} error={errors.next} />
      {strength === 'weak' ? <Text style={styles.hint}>{profileMessages.strengthWeak}</Text> : null}
      {strength === 'good' ? <Text style={styles.good}>{profileMessages.strengthGood}</Text> : null}
      <Secret label={profileMessages.confirmPassword} value={confirm} onChangeText={setConfirm} error={errors.confirm} />
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.save} disabled={pending} onPress={() => void save()} style={styles.button}>
        <Text style={styles.buttonLabel}>{pending ? profileMessages.saving : profileMessages.save}</Text>
      </Pressable>
    </View>
  );
}

function Secret({ label, value, onChangeText, error }: { label: string; value: string; onChangeText: (value: string) => void; error?: string }) {
  return (
    <View style={styles.field}>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        secureTextEntry
        accessibilityLabel={label}
        placeholder={label}
        placeholderTextColor={theme.colors.textSecondary}
        keyboardAppearance="dark"
        style={styles.input}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, gap: 12, paddingHorizontal: 24, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  field: { gap: 6 },
  input: {
    minHeight: 48,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    paddingHorizontal: 12,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  hint: { color: theme.colors.danger, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  good: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  row: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
