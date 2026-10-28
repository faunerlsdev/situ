import { AppError, callingCodes, composePhone, notify, phonePurpose, profileMessages } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { SelectField } from '@/components/select-field';
import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const callingCodeOptions = callingCodes.map((item) => ({ id: item.code, name: item.label }));

import {
  isValidUserName,
  normalizeUserName,
  usernameAvailabilityMessage,
  usernameCooldownLabel,
  validateProfileForm,
} from '@/features/account/logic';

export default function EditProfileScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const me = useMe();
  const profile = me.data;
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [bio, setBio] = useState('');
  const [cityId, setCityId] = useState<string | null>(null);
  const [cityName, setCityName] = useState('');
  const [available, setAvailable] = useState<boolean | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [pending, setPending] = useState(false);
  const [callingCode, setCallingCode] = useState('57');
  const [phone, setPhone] = useState('');
  const [phonePassword, setPhonePassword] = useState('');
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (!profile || ready) {
      return;
    }
    setUserName(profile.userName);
    setDisplayName(profile.displayName);
    setBio(profile.bio ?? '');
    if (profile.homeCity) {
      setCityId(profile.homeCity.id);
      setCityName(profile.homeCity.name);
    }
    setReady(true);
  }, [profile, ready]);

  const cooldown = profile?.canChangeUserNameAt ?? null;
  const lockedName = cooldown !== null && new Date(cooldown).getTime() > Date.now();

  useEffect(() => {
    if (!ready || lockedName) {
      return;
    }
    const normalized = normalizeUserName(userName);
    if (!isValidUserName(normalized) || normalized === profile?.userName) {
      setAvailable(null);
      return;
    }
    const handle = setTimeout(() => {
      void api.usernameAvailable(normalized).then((result) => setAvailable(result.available)).catch(() => setAvailable(null));
    }, 400);
    return () => clearTimeout(handle);
  }, [lockedName, profile?.userName, ready, userName]);

  async function save() {
    const nextErrors = validateProfileForm({ userName, displayName, bio });
    if (lockedName && normalizeUserName(userName) !== profile?.userName) {
      nextErrors.userName = usernameCooldownLabel(cooldown ?? '');
    }
    if (available === false) {
      nextErrors.userName = profileMessages.taken;
    }
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      return;
    }
    setPending(true);
    try {
      await api.updateMyProfile({
        userName: lockedName ? null : normalizeUserName(userName),
        displayName: displayName.trim(),
        bio: bio.trim(),
        homeCityId: cityId,
      });
      await queryClient.invalidateQueries({ queryKey: ['me'] });
      notify.success(profileMessages.saved);
      router.back();
    } catch (error) {
      if (error instanceof AppError && error.apiCode === 'profile.username_change_limited') {
        setErrors({ userName: usernameCooldownLabel(error.retryAfter ?? cooldown ?? '') });
        return;
      }
      if (error instanceof AppError && error.apiCode === 'user.username_taken') {
        setErrors({ userName: profileMessages.taken });
        return;
      }
      notify.error(error, { action: { label: profileMessages.retry, onPress: () => void save() } });
    } finally {
      setPending(false);
    }
  }

  async function savePhone() {
    if (!phone.trim() || !phonePassword) {
      notify.warning('Escribe el número y tu contraseña actual.');
      return;
    }
    try {
      await api.changePhone({ currentPassword: phonePassword, phone: composePhone(callingCode, phone) });
      setPhonePassword('');
      notify.success('Actualizamos tu teléfono.');
      await queryClient.invalidateQueries({ queryKey: ['me'] });
    } catch (error) {
      notify.error(error);
    }
  }

  if (me.isError) {
    return (
      <View style={[styles.screen, { paddingTop: insets.top + 12 }]}>
        <Text style={styles.body}>{profileMessages.loadError}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.retry} onPress={() => void me.refetch()} style={styles.row}>
          <Text style={styles.link}>{profileMessages.retry}</Text>
        </Pressable>
      </View>
    );
  }

  const hint = lockedName ? usernameCooldownLabel(cooldown ?? '') : usernameAvailabilityMessage(available);

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 12, paddingBottom: insets.bottom + 24 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.back} onPress={() => router.back()} style={styles.row}>
        <Text style={styles.link}>{profileMessages.back}</Text>
      </Pressable>
      <Text style={styles.title}>{profileMessages.edit}</Text>
      <Field label={profileMessages.userName} value={userName} onChangeText={setUserName} editable={!lockedName} error={errors.userName} hint={hint} autoCapitalize="none" />
      <Field label={profileMessages.displayName} value={displayName} onChangeText={setDisplayName} error={errors.displayName} />
      <Field label={profileMessages.bio} value={bio} onChangeText={setBio} error={errors.bio} multiline />
      <Text style={styles.counter}>{`${bio.trim().length}/160`}</Text>
      <Text style={styles.label}>{profileMessages.city}</Text>
      <Text style={styles.body}>{cityName}</Text>
      <CityPicker
        onPick={(id, name) => {
          setCityId(id);
          setCityName(name);
        }}
      />
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.save} disabled={pending} onPress={() => void save()} style={styles.button}>
        <Text style={styles.buttonLabel}>{pending ? profileMessages.saving : profileMessages.save}</Text>
      </Pressable>
      <Text style={styles.title}>Teléfono</Text>
      <Text style={styles.body}>{phonePurpose}</Text>
      <SelectField
        label="Indicativo"
        value={callingCode}
        placeholder="Selecciona un país"
        options={callingCodeOptions}
        onChange={setCallingCode}
      />
      <Field label="Número" value={phone} onChangeText={setPhone} autoCapitalize="none" />
      <Field label="Contraseña actual" value={phonePassword} onChangeText={setPhonePassword} autoCapitalize="none" />
      <Pressable accessibilityRole="button" accessibilityLabel="Guardar teléfono" onPress={() => void savePhone()} style={styles.button}>
        <Text style={styles.buttonLabel}>Guardar teléfono</Text>
      </Pressable>
    </ScrollView>
  );
}

function Field({
  label,
  value,
  onChangeText,
  error,
  hint,
  editable = true,
  multiline = false,
  autoCapitalize = 'sentences',
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  error?: string;
  hint?: string | null;
  editable?: boolean;
  multiline?: boolean;
  autoCapitalize?: 'none' | 'sentences';
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        editable={editable}
        multiline={multiline}
        accessibilityLabel={label}
        autoCapitalize={autoCapitalize}
        autoCorrect={false}
        placeholderTextColor={theme.colors.textSecondary}
        keyboardAppearance="dark"
        style={[styles.input, multiline && styles.multiline]}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {!error && hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

function CityPicker({ onPick }: { onPick: (id: string, name: string) => void }) {
  const countries = useQuery({ queryKey: ['countries'], queryFn: () => api.listCountries() });
  const [countryId, setCountryId] = useState<string | null>(null);
  const [regionId, setRegionId] = useState<string | null>(null);
  const regions = useQuery({ queryKey: ['regions', countryId], enabled: countryId !== null, queryFn: () => api.listRegions(countryId ?? '') });
  const cities = useQuery({ queryKey: ['cities', regionId], enabled: regionId !== null, queryFn: () => api.listCities(regionId ?? '') });

  if (countries.isError) {
    return (
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.retry} onPress={() => void countries.refetch()} style={styles.row}>
        <Text style={styles.link}>{profileMessages.retry}</Text>
      </Pressable>
    );
  }

  return (
    <View style={styles.field}>
      <Text style={styles.label}>{profileMessages.country}</Text>
      {(countries.data?.countries ?? []).filter((item) => item.isEnabled).map((item) => (
        <Pressable key={item.id} accessibilityRole="button" accessibilityLabel={item.name} onPress={() => { setCountryId(item.id); setRegionId(null); }} style={styles.row}>
          <Text style={[styles.body, item.id === countryId && styles.link]}>{item.name}</Text>
        </Pressable>
      ))}
      {countryId ? <Text style={styles.label}>{profileMessages.region}</Text> : null}
      {(regions.data?.regions ?? []).map((item) => (
        <Pressable key={item.id} accessibilityRole="button" accessibilityLabel={item.name} onPress={() => setRegionId(item.id)} style={styles.row}>
          <Text style={[styles.body, item.id === regionId && styles.link]}>{item.name}</Text>
        </Pressable>
      ))}
      {(cities.data?.cities ?? []).map((item) => (
        <Pressable key={item.id} accessibilityRole="button" accessibilityLabel={item.name} onPress={() => onPick(item.id, item.name)} style={styles.row}>
          <Text style={styles.body}>{item.name}</Text>
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 24 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  field: { gap: 6 },
  label: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
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
  multiline: { minHeight: 96, paddingTop: 12, textAlignVertical: 'top' },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  hint: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14 },
  counter: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, textAlign: 'right' },
  row: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
