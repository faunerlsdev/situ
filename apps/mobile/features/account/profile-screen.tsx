import { profileMessages } from '@situ/shared';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { useMe } from '@/features/reports/use-me';
import { theme } from '@/theme/theme';

import { AvatarSheet } from './avatar-sheet';
import { memberSinceLabel } from './logic';

export function ProfileScreen() {
  const router = useRouter();
  const me = useMe();
  const [sheet, setSheet] = useState(false);
  const profile = me.data;

  if (me.isError) {
    return (
      <View style={styles.screen}>
        <Text style={styles.body}>{profileMessages.loadError}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.retry} onPress={() => void me.refetch()} style={styles.button}>
          <Text style={styles.buttonLabel}>{profileMessages.retry}</Text>
        </Pressable>
      </View>
    );
  }

  if (!profile) {
    return <View style={styles.skeleton} />;
  }

  const letter = (profile.displayName || profile.userName || 'S').slice(0, 1).toUpperCase();

  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.avatar} onPress={() => setSheet(true)} style={styles.avatar}>
        {profile.avatar.url ? <Image source={{ uri: profile.avatar.url }} style={styles.avatarImage} /> : <Text style={styles.letter}>{letter}</Text>}
      </Pressable>
      <Text style={styles.title}>{profile.displayName}</Text>
      <Text style={styles.handle}>@{profile.userName}</Text>
      {profile.bio ? <Text style={styles.body}>{profile.bio}</Text> : null}
      <Text style={styles.meta}>{memberSinceLabel(profile.memberSinceYear, profile.memberSinceMonth)}</Text>
      <Text style={styles.meta}>{profile.homeCity.name}</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.edit} onPress={() => router.push('/editar-perfil')} style={styles.button}>
        <Text style={styles.buttonLabel}>{profileMessages.edit}</Text>
      </Pressable>
      <AvatarSheet visible={sheet} isMinor={profile.isMinor} onClose={() => setSheet(false)} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 8, padding: 24, paddingBottom: 48 },
  skeleton: { flex: 1, backgroundColor: theme.colors.bg },
  avatar: { width: 88, height: 88, borderRadius: 44, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surface },
  avatarImage: { width: 88, height: 88, borderRadius: 44 },
  letter: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 32 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  handle: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  meta: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  button: { minHeight: 44, marginTop: 12, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
