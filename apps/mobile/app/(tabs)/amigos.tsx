import type { FriendHistoryItem, FriendListItem, FriendRequestItem, UserCard } from '@situ/shared';
import { messageFromUnknown, notify } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { friendActionLabel, pendingRequestCount } from '@/features/social/logic';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

type Tab = 'friends' | 'requests' | 'search';
type HistoryFilter = 'Pending' | 'Accepted' | 'Declined';

const historyCopy: Record<HistoryFilter, { label: string; empty: string }> = {
  Pending: { label: 'Pendientes', empty: 'No hay solicitudes pendientes.' },
  Accepted: { label: 'Aceptadas', empty: 'Todavía no hay solicitudes aceptadas.' },
  Declined: { label: 'Rechazadas', empty: 'No hay solicitudes rechazadas.' },
};

export default function FriendsScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>('friends');
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [inviteOpen, setInviteOpen] = useState(false);
  const [found, setFound] = useState<UserCard[] | null>(null);
  const [searching, setSearching] = useState(false);
  const [historyFilter, setHistoryFilter] = useState<HistoryFilter>('Pending');
  const friends = useQuery({ queryKey: ['friends'], queryFn: () => api.friends() });
  const requests = useQuery({ queryKey: ['friend-requests'], queryFn: () => api.friendRequests() });
  const history = useQuery({
    queryKey: ['friend-history', historyFilter],
    queryFn: () => api.friendHistory(historyFilter),
    enabled: tab === 'requests' && historyFilter !== 'Pending',
    retry: false,
  });
  const pending = pendingRequestCount(requests.data?.incoming);

  const refresh = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['friends'] });
    await queryClient.invalidateQueries({ queryKey: ['friend-requests'] });
    await queryClient.invalidateQueries({ queryKey: ['friend-history'] });
  }, [queryClient]);

  useFocusEffect(useCallback(() => {
    void refresh();
  }, [refresh]));

  useEffect(() => {
    const name = userName.trim();
    if (tab !== 'search' || name.length < 3) {
      return;
    }
    const timer = setTimeout(() => {
      void runSearch(name);
    }, 400);
    return () => clearTimeout(timer);
  }, [tab, userName]);

  async function runSearch(name: string) {
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
    } catch (error) {
      notify.error(error);
    }
  }

  async function add(userId: string) {
    try {
      const result = await api.sendFriendRequest(userId);
      notify.success(result.status === 'incoming' ? 'Esa persona ya te escribió. Revisa Solicitudes.' : 'Solicitud enviada.');
      await refresh();
      if (userName.trim().length >= 3) {
        await runSearch(userName.trim());
      }
    } catch (error) {
      notify.error(error);
    }
  }

  async function act(kind: 'accept' | 'reject' | 'cancel', id: string) {
    try {
      if (kind === 'accept') {
        await api.acceptFriendRequest(id);
        notify.success('Aceptaste la solicitud.');
      } else if (kind === 'reject') {
        await api.rejectFriendRequest(id);
        notify.success('Rechazaste la solicitud.');
      } else {
        await api.cancelFriendRequest(id);
        notify.success('Cancelaste la solicitud.');
      }
      await refresh();
    } catch (error) {
      notify.error(error, { action: { label: 'Reintentar', onPress: () => void act(kind, id) } });
    }
  }

  const loading = friends.isLoading || requests.isLoading;
  const failed = friends.isError || requests.isError;

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: 12, paddingBottom: insets.bottom + 32 }]} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>Amigos</Text>
      <View style={styles.tabs}>
        <TabButton label="Amigos" selected={tab === 'friends'} onPress={() => setTab('friends')} />
        <TabButton label="Solicitudes" selected={tab === 'requests'} badge={pending} onPress={() => setTab('requests')} />
        <TabButton label="Buscar" selected={tab === 'search'} onPress={() => setTab('search')} />
      </View>

      {loading ? <View style={styles.skeleton} /> : null}
      {failed ? (
        <View style={styles.card}>
          <Text style={styles.body}>{messageFromUnknown(friends.error ?? requests.error)}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void refresh()} style={styles.textButton}>
            <Text style={styles.link}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}

      {tab === 'friends' && !loading && !failed ? (
        <>
          {(friends.data?.friends.length ?? 0) === 0 ? <Text style={styles.body}>Todavía no tienes amigos en SITU.</Text> : null}
          {friends.data?.friends.map((item) => (
            <FriendRow key={item.userId} item={item} onPress={() => router.push(`/usuario/${item.userName}`)} />
          ))}
        </>
      ) : null}

      {tab === 'requests' && !loading && !failed ? (
        <>
          <Text style={styles.heading}>Recibidas</Text>
          {(requests.data?.incoming.length ?? 0) === 0 ? <Text style={styles.body}>No tienes solicitudes nuevas.</Text> : null}
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
          <Text style={styles.heading}>Enviadas</Text>
          {(requests.data?.outgoing.length ?? 0) === 0 ? <Text style={styles.body}>No tienes solicitudes en camino.</Text> : null}
          {requests.data?.outgoing.map((item) => (
            <View key={item.requestId} style={styles.card}>
              <Person name={item.displayName} userName={item.userName} avatarUrl={item.avatarUrl} />
              <Pressable accessibilityRole="button" accessibilityLabel={`Cancelar solicitud a ${item.userName}`} onPress={() => void act('cancel', item.requestId)} style={styles.textButton}>
                <Text style={styles.danger}>Cancelar</Text>
              </Pressable>
            </View>
          ))}
          <Text style={styles.heading}>Historial</Text>
          <View style={styles.tabs}>
            {(Object.keys(historyCopy) as HistoryFilter[]).map((filter) => (
              <TabButton key={filter} label={historyCopy[filter].label} selected={historyFilter === filter} onPress={() => setHistoryFilter(filter)} />
            ))}
          </View>
          {historyFilter === 'Pending' ? <Text style={styles.body}>Las pendientes están en Recibidas y Enviadas.</Text> : null}
          {historyFilter !== 'Pending' && history.isError ? <Text style={styles.body}>{messageFromUnknown(history.error)}</Text> : null}
          {historyFilter !== 'Pending' && history.isLoading ? <View style={styles.skeleton} /> : null}
          {historyFilter !== 'Pending' && !history.isLoading && !history.isError && (history.data?.items.length ?? 0) === 0 ? <Text style={styles.body}>{historyCopy[historyFilter].empty}</Text> : null}
          {historyFilter !== 'Pending' ? history.data?.items.map((item) => (
            <HistoryRow key={item.requestId} item={item} onPress={() => router.push(`/usuario/${item.userName}`)} />
          )) : null}
        </>
      ) : null}

      {tab === 'search' ? (
        <>
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
          {searching ? <Text style={styles.body}>Buscando…</Text> : null}
          {found && found.length === 0 && userName.trim().length >= 3 && !searching ? <Text style={styles.body}>No encontramos a nadie con ese usuario.</Text> : null}
          {found?.map((person) => {
            const action = friendActionLabel(person.relation);
            return (
              <View key={person.userName} style={styles.card}>
                <Person name={person.displayName} userName={person.userName} avatarUrl={person.avatarUrl} />
                <View style={styles.row}>
                  <Pressable accessibilityRole="button" accessibilityLabel={`Ver perfil de ${person.userName}`} onPress={() => router.push(`/usuario/${person.userName}`)} style={styles.textButton}>
                    <Text style={styles.link}>Ver perfil</Text>
                  </Pressable>
                  <Pressable
                    accessibilityRole="button"
                    accessibilityState={{ disabled: !action.enabled }}
                    accessibilityLabel={`${action.label} ${person.userName}`}
                    disabled={!action.enabled}
                    onPress={() => void add(person.userId)}
                    style={styles.textButton}
                  >
                    <Text style={action.enabled ? styles.link : styles.body}>{action.label}</Text>
                  </Pressable>
                </View>
              </View>
            );
          })}
          <Pressable accessibilityRole="button" accessibilityLabel="Invitar por correo" onPress={() => setInviteOpen((value) => !value)} style={styles.textButton}>
            <Text style={styles.link}>Invitar por correo</Text>
          </Pressable>
          {inviteOpen ? (
            <>
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
            </>
          ) : null}
        </>
      ) : null}
    </ScrollView>
  );
}

function TabButton({ label, selected, badge = 0, onPress }: { label: string; selected: boolean; badge?: number; onPress: () => void }) {
  const count = badge > 99 ? '99+' : String(badge);
  return (
    <Pressable accessibilityRole="tab" accessibilityState={{ selected }} accessibilityLabel={badge > 0 ? `${label}, ${badge} pendientes` : label} onPress={onPress} style={[styles.tab, selected && styles.tabOn]}>
      <Text style={styles.tabLabel}>{label}</Text>
      {badge > 0 ? (
        <View style={styles.countBadge} accessibilityElementsHidden>
          <Text style={styles.countBadgeText}>{count}</Text>
        </View>
      ) : null}
    </Pressable>
  );
}

function FriendRow({ item, onPress }: { item: FriendListItem; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`Abrir perfil de ${item.userName}`} onPress={onPress} style={styles.card}>
      <Person name={item.displayName} userName={item.userName} avatarUrl={item.avatarUrl} />
      <Text style={styles.body}>{item.sharesLocation ? 'Comparte ubicación contigo' : 'No comparte ubicación'}</Text>
    </Pressable>
  );
}

function HistoryRow({ item, onPress }: { item: FriendHistoryItem; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`Abrir perfil de ${item.userName}`} onPress={onPress} style={styles.card}>
      <Person name={item.displayName} userName={item.userName} avatarUrl={item.avatarUrl} />
      <Text style={styles.body}>{item.status === 'Accepted' ? 'Aceptada' : item.status === 'Declined' ? 'Rechazada' : 'Pendiente'}</Text>
    </Pressable>
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
  avatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: theme.colors.skeleton, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  row: { flexDirection: 'row', gap: 16 },
  textButton: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  danger: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  skeleton: { height: 72, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  tabs: { flexDirection: 'row', gap: 8 },
  tab: { minHeight: 44, paddingHorizontal: 12, borderRadius: 22, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, backgroundColor: theme.colors.surface },
  tabOn: { backgroundColor: theme.colors.primary },
  tabLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  countBadge: { minWidth: 20, height: 20, paddingHorizontal: 5, borderRadius: 10, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.dangerSolid },
  countBadgeText: { color: theme.colors.textOnDanger, fontFamily: theme.fontFamily.sansBold, fontSize: 12 },
});
