import { messageFromUnknown, notify } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function FriendsScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [searching, setSearching] = useState(false);
  const [found, setFound] = useState<{ userName: string; displayName: string; avatarUrl: string | null }[] | null>(null);
  const friends = useQuery({ queryKey: ['friends'], queryFn: () => api.friends() });
  const requests = useQuery({ queryKey: ['friend-requests'], queryFn: () => api.friendRequests() });

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['friends'] });
    await queryClient.invalidateQueries({ queryKey: ['friend-requests'] });
  }

  async function search() {
    const name = userName.trim();
    if (name.length < 3) {
      notify.warning('Escribe el nombre de usuario completo, de al menos 3 caracteres.');
      return;
    }
    setSearching(true);
    try {
      const result = await api.searchUsers(name);
      setFound(result.users);
    } catch (error) {
      setFound(null);
      notify.error(error);
    } finally {
      setSearching(false);
    }
  }

  async function invite() {
    const address = email.trim();
    if (address.length === 0) {
      notify.warning('Escribe un correo.');
      return;
    }
    try {
      await api.inviteFriend(address);
      setEmail('');
      notify.success('Si el correo puede recibir la invitación, le escribimos. La respuesta es la misma si la persona ya tiene cuenta.');
      await refresh();
    } catch (error) {
      notify.error(error);
    }
  }

  async function act(kind: 'accept' | 'reject' | 'cancel', id: string) {
    try {
      if (kind === 'accept') {
        await api.acceptFriendRequest(id);
      } else if (kind === 'reject') {
        await api.rejectFriendRequest(id);
      } else {
        await api.cancelFriendRequest(id);
      }
      await refresh();
    } catch (error) {
      notify.error(error, { action: { label: 'Reintentar', onPress: () => void act(kind, id) } });
    }
  }

  const loading = friends.isLoading || requests.isLoading;
  const failed = friends.isError || requests.isError;

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 12, paddingBottom: insets.bottom + 32 }]}>
      <Text style={styles.title}>Amigos</Text>
      <Text style={styles.body}>Busca por el nombre de usuario exacto. Un menor solo aparece si escribes ese usuario completo.</Text>
      <TextInput
        value={userName}
        onChangeText={setUserName}
        autoCapitalize="none"
        autoCorrect={false}
        accessibilityLabel="Nombre de usuario"
        placeholder="Nombre de usuario"
        placeholderTextColor={theme.colors.textSecondary}
        keyboardAppearance="dark"
        style={styles.input}
      />
      <Pressable accessibilityRole="button" accessibilityLabel="Buscar" disabled={searching} onPress={() => void search()} style={styles.button}>
        <Text style={styles.buttonLabel}>{searching ? 'Buscando…' : 'Buscar'}</Text>
      </Pressable>
      {found && found.length === 0 ? <Text style={styles.body}>No encontramos a nadie con ese usuario.</Text> : null}
      {found?.map((person) => (
        <View key={person.userName} style={styles.card}>
          <Person name={person.displayName} userName={person.userName} avatarUrl={person.avatarUrl} />
          <Pressable accessibilityRole="button" accessibilityLabel={`Ver perfil de ${person.userName}`} onPress={() => router.push(`/usuario/${person.userName}`)} style={styles.textButton}>
            <Text style={styles.link}>Ver perfil</Text>
          </Pressable>
        </View>
      ))}

      <Text style={styles.heading}>Invitar por correo</Text>
      <TextInput
        value={email}
        onChangeText={setEmail}
        autoCapitalize="none"
        autoCorrect={false}
        keyboardType="email-address"
        accessibilityLabel="Correo de la invitación"
        placeholder="correo@ejemplo.com"
        placeholderTextColor={theme.colors.textSecondary}
        keyboardAppearance="dark"
        style={styles.input}
      />
      <Pressable accessibilityRole="button" accessibilityLabel="Enviar invitación" onPress={() => void invite()} style={styles.button}>
        <Text style={styles.buttonLabel}>Enviar invitación</Text>
      </Pressable>

      {loading ? <View style={styles.skeleton} /> : null}
      {failed ? (
        <View style={styles.card}>
          <Text style={styles.body}>{messageFromUnknown(friends.error ?? requests.error)}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void refresh()} style={styles.textButton}>
            <Text style={styles.link}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}

      <Text style={styles.heading}>Solicitudes recibidas</Text>
      {!loading && !failed && (requests.data?.incoming.length ?? 0) === 0 ? <Text style={styles.body}>No tienes solicitudes nuevas.</Text> : null}
      {requests.data?.incoming.map((item) => (
        <View key={item.requestId} style={styles.card}>
          <Person name={item.displayName} userName={item.userName} avatarUrl={item.avatarUrl} />
          <View style={styles.row}>
            <Pressable accessibilityRole="button" accessibilityLabel={`Aceptar a ${item.userName}`} onPress={() => void act('accept', item.requestId)} style={styles.textButton}>
              <Text style={styles.link}>Aceptar</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel={`Rechazar a ${item.userName}`} onPress={() => void act('reject', item.requestId)} style={styles.textButton}>
              <Text style={styles.danger}>Rechazar</Text>
            </Pressable>
          </View>
        </View>
      ))}

      <Text style={styles.heading}>Solicitudes enviadas</Text>
      {!loading && !failed && (requests.data?.outgoing.length ?? 0) === 0 ? <Text style={styles.body}>No tienes solicitudes en camino.</Text> : null}
      {requests.data?.outgoing.map((item) => (
        <View key={item.requestId} style={styles.card}>
          <Person name={item.displayName} userName={item.userName} avatarUrl={item.avatarUrl} />
          <Pressable accessibilityRole="button" accessibilityLabel={`Cancelar solicitud a ${item.userName}`} onPress={() => void act('cancel', item.requestId)} style={styles.textButton}>
            <Text style={styles.danger}>Cancelar</Text>
          </Pressable>
        </View>
      ))}

      <Text style={styles.heading}>Tus amigos</Text>
      {!loading && !failed && (friends.data?.friends.length ?? 0) === 0 ? <Text style={styles.body}>Todavía no tienes amigos en SITU.</Text> : null}
      {friends.data?.friends.map((item) => (
        <Pressable key={item.userId} accessibilityRole="button" accessibilityLabel={`Abrir perfil de ${item.userName}`} onPress={() => router.push(`/usuario/${item.userName}`)} style={styles.card}>
          <Person name={item.displayName} userName={item.userName} avatarUrl={item.avatarUrl} />
          <Text style={styles.body}>{item.sharesLocation ? 'Comparte ubicación contigo' : 'No comparte ubicación'}</Text>
        </Pressable>
      ))}
    </ScrollView>
  );
}

function Person({ name, userName, avatarUrl }: { name: string; userName: string; avatarUrl: string | null }) {
  return (
    <View style={styles.person}>
      {avatarUrl ? <Image source={{ uri: avatarUrl }} style={styles.avatar} accessibilityIgnoresInvertColors /> : <View style={styles.avatar} />}
      <View>
        <Text style={styles.name}>{name}</Text>
        <Text style={styles.body}>@{userName}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 20 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  heading: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 20, marginTop: 8 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  name: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
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
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  card: { gap: 8, padding: 12, borderRadius: 12, backgroundColor: theme.colors.surface },
  person: { flexDirection: 'row', gap: 12, alignItems: 'center' },
  avatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: theme.colors.skeleton },
  row: { flexDirection: 'row', gap: 16 },
  textButton: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  danger: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  skeleton: { height: 72, borderRadius: 12, backgroundColor: theme.colors.skeleton },
});
