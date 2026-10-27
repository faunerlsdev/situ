import { notificationMessages, notify, profileMessages, shellTabHrefs, socialTabsFrom } from '@situ/shared';
import { useQuery } from '@tanstack/react-query';
import { useRouter, usePathname } from 'expo-router';
import * as Location from 'expo-location';
import { createContext, useContext, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Image, Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { setMapPlace } from '@/features/reports/place';
import { useAppConfig } from '@/features/reports/use-app-config';
import { api } from '@/lib/api';

import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

const wordmark = require('../assets/images/brand/situ-wordmark-naranja.png');
const searchIcon = require('../assets/images/icons/ic-buscar.png');
const newsIcon = require('../assets/images/icons/situ-noticias-dark.png');
const mapIcon = require('../assets/images/icons/nav-mapa-globo-dark.png');
const chismeIcon = require('../assets/images/icons/nav-chisme-megafono-dark.png');
const liveIcon = require('../assets/images/icons/nav-en-vivo-dark.png');
const messagesIcon = require('../assets/images/icons/nav-mensajes-dark.png');

type ShellContextValue = {
  openDrawer: () => void;
  openSearch: () => void;
};

const ShellContext = createContext<ShellContextValue | null>(null);

const tabs = [
  { href: '/mapa', label: 'Mapa', icon: mapIcon },
  { href: '/chisme', label: 'Chisme', icon: chismeIcon },
  { href: '/notificaciones', label: notificationMessages.tab, icon: liveIcon },
  { href: '/mensajes', label: 'Mensajes', icon: messagesIcon },
] as const;

export function ShellProvider({ children }: { children: ReactNode }) {
  const [drawer, setDrawer] = useState(false);
  const [search, setSearch] = useState(false);
  const value = useMemo(
    () => ({
      openDrawer: () => setDrawer(true),
      openSearch: () => setSearch(true),
    }),
    [],
  );

  return (
    <ShellContext.Provider value={value}>
      <View style={styles.fill}>{children}</View>
      <AccountDrawer visible={drawer} onClose={() => setDrawer(false)} />
      <SearchSheet visible={search} onClose={() => setSearch(false)} />
    </ShellContext.Provider>
  );
}

export function WazeHeader() {
  const insets = useSafeAreaInsets();
  const shell = useContext(ShellContext);

  return (
    <View style={{ backgroundColor: theme.colors.surface, paddingTop: insets.top }}>
      <View style={styles.header}>
        <View style={styles.headerLeft}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Abrir menú"
            onPress={() => shell?.openDrawer()}
            style={styles.avatar}
          >
            <Text style={styles.avatarText}>S</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Buscar" onPress={() => shell?.openSearch()}>
            <Image source={searchIcon} resizeMode="contain" style={styles.search} />
          </Pressable>
        </View>
        <Image source={wordmark} resizeMode="contain" accessibilityLabel="Situ" style={styles.wordmark} />
      </View>
      <View style={styles.divider} />
    </View>
  );
}

export function WazeTabBar() {
  const insets = useSafeAreaInsets();
  const pathname = usePathname();
  const router = useRouter();
  const config = useAppConfig();
  const visible = tabs.filter((tab) => shellTabHrefs(socialTabsFrom(config.data)).includes(tab.href));

  return (
    <View style={{ backgroundColor: theme.colors.surface, paddingBottom: insets.bottom }}>
      <View style={styles.divider} />
      <View style={styles.tabs}>
        {visible.map((tab) => {
          const active = pathname === tab.href;
          return (
            <Pressable
              key={tab.href}
              accessibilityRole="tab"
              accessibilityLabel={tab.label}
              accessibilityState={{ selected: active }}
              onPress={() => router.navigate(tab.href)}
              style={styles.tab}
            >
              <View>
                <Image source={tab.icon} resizeMode="contain" style={[styles.tabIcon, !active && styles.inactive]} />
                {tab.href === '/notificaciones' ? <UnreadBadge /> : null}
              </View>
              {active ? <View style={styles.dot} /> : <View style={styles.dotSpacer} />}
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

function UnreadBadge() {
  const unread = useQuery({ queryKey: ['notifications', 'unread'], queryFn: () => api.unreadNotificationCount() });
  const count = Number(unread.data?.count ?? 0);
  if (count <= 0) {
    return null;
  }

  return (
    <View style={styles.badge} accessibilityLabel={`${count} sin leer`}>
      <Text style={styles.badgeText}>{count > 99 ? '99+' : String(count)}</Text>
    </View>
  );
}

function AccountDrawer({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const router = useRouter();
  const { signOut } = useSession();
  const insets = useSafeAreaInsets();
  const sharing = useQuery({ queryKey: ['location-sharing'], queryFn: () => api.locationSharing(), enabled: visible });

  function go(href: '/perfil' | '/amigos' | '/visibilidad' | '/ubicacion' | '/reportes' | '/bloqueados' | '/cuenta' | '/ajustes') {
    onClose();
    router.push(href);
  }

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.modalFill}>
        <Pressable accessibilityLabel="Cerrar menú" style={styles.backdrop} onPress={onClose} />
        <View style={[styles.drawer, { paddingTop: insets.top + 24 }, theme.shadow]}>
          <Text style={styles.drawerTitle}>Mi SITU</Text>
          <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.menu} onPress={() => go('/perfil')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>{profileMessages.menu}</Text>
          </Pressable>
          <Pressable accessibilityRole="button" onPress={() => go('/amigos')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Amigos</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Visibilidad de mi perfil" onPress={() => go('/visibilidad')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Visibilidad de mi perfil</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Compartir mi ubicación" onPress={() => go('/ubicacion')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>{sharing.data?.enabled && !sharing.data.ghost ? 'Compartir mi ubicación · activo' : 'Compartir mi ubicación'}</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Mis reportes" onPress={() => go('/reportes')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Mis reportes</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Privacidad" onPress={() => go('/bloqueados')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Privacidad</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Mi cuenta" onPress={() => go('/cuenta')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Mi cuenta</Text>
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.settingsMenu} onPress={() => go('/ajustes')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>{notificationMessages.settingsMenu}</Text>
          </Pressable>
          <Pressable
            accessibilityRole="button"
            onPress={() => {
              onClose();
              void signOut();
            }}
            style={styles.drawerRow}
          >
            <Text style={styles.drawerLabel}>Cerrar sesión</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

function SearchSheet({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const insets = useSafeAreaInsets();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<{ latitude: number; longitude: number; label: string }[]>([]);
  const [searching, setSearching] = useState(false);

  async function search() {
    const text = query.trim();
    if (text.length === 0) {
      return;
    }

    setSearching(true);
    try {
      const found = await Location.geocodeAsync(text);
      if (found.length === 0) {
        setResults([]);
        notify.warning('No encontramos ese lugar.');
        return;
      }

      const labeled = await Promise.all(found.slice(0, 5).map(async (item, index) => {
        let label = text;
        try {
          const reversed = await Location.reverseGeocodeAsync(item);
          const first = reversed[0];
          const parts = first ? [first.name, first.street, first.city, first.region].filter((part) => part && part.length > 0) : [];
          if (parts.length > 0) {
            label = parts.join(', ');
          }
        } catch {
          label = `${text} (${index + 1})`;
        }
        return { latitude: item.latitude, longitude: item.longitude, label };
      }));
      setResults(labeled);
    } catch {
      notify.warning('No pudimos buscar esa dirección.');
    } finally {
      setSearching(false);
    }
  }

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={[styles.searchScreen, { paddingTop: insets.top + 12 }]}>
        <TextInput
          keyboardAppearance="dark"
          autoFocus
          value={query}
          onChangeText={setQuery}
          onSubmitEditing={() => void search()}
          placeholder="Buscar dirección o lugar"
          placeholderTextColor={theme.colors.textPlaceholder}
          accessibilityLabel="Buscar dirección o lugar"
          style={styles.searchInput}
        />
        <Pressable accessibilityRole="button" accessibilityLabel="Cerrar búsqueda" onPress={onClose}>
          <Text style={styles.cancel}>Cerrar</Text>
        </Pressable>
        <Pressable accessibilityRole="button" accessibilityLabel="Buscar" onPress={() => void search()} style={styles.drawerRow}>
          <Text style={styles.drawerLabel}>{searching ? 'Buscando…' : 'Buscar'}</Text>
        </Pressable>
        {results.map((item) => (
          <Pressable
            key={`${item.latitude}-${item.longitude}-${item.label}`}
            accessibilityRole="button"
            accessibilityLabel={item.label}
            onPress={() => {
              setMapPlace(item);
              onClose();
            }}
            style={styles.drawerRow}
          >
            <Text style={styles.drawerLabel}>{item.label}</Text>
          </Pressable>
        ))}
      </View>
    </Modal>
  );
}

export function EmptyState({ title }: { title: string }) {
  return (
    <View style={styles.emptyPage}>
      <Image source={newsIcon} resizeMode="contain" style={styles.news} />
      <Text style={styles.emptyTitle}>{title}</Text>
      <Text style={styles.soon}>Próximamente</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: {
    flex: 1,
  },
  header: {
    height: 56,
    paddingHorizontal: 16,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: theme.colors.surface,
  },
  headerLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  avatar: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.5,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.surfaceElevated,
  },
  avatarText: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  search: {
    width: 32,
    height: 32,
  },
  wordmark: {
    height: 28,
    width: 72,
  },
  divider: {
    height: 1,
    backgroundImage: `linear-gradient(90deg, transparent, ${theme.colors.primary}, transparent)`,
  },
  tabs: {
    height: 64,
    flexDirection: 'row',
    backgroundColor: theme.colors.surface,
  },
  tab: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
  },
  tabIcon: {
    width: 32,
    height: 32,
  },
  inactive: {
    opacity: 0.5,
  },
  dot: {
    width: 4,
    height: 4,
    borderRadius: 2,
    backgroundColor: theme.colors.primary,
  },
  dotSpacer: {
    width: 4,
    height: 4,
  },
  badge: {
    position: 'absolute',
    top: -4,
    right: -10,
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    paddingHorizontal: 4,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.dangerSolid,
  },
  badgeText: {
    color: theme.colors.textOnDanger,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 11,
  },
  modalFill: {
    flex: 1,
    flexDirection: 'row',
  },
  backdrop: {
    ...StyleSheet.absoluteFill,
    backgroundColor: theme.colors.overlay,
  },
  drawer: {
    width: 280,
    height: '100%',
    backgroundColor: theme.colors.surfaceElevated,
    paddingHorizontal: 20,
    gap: 8,
  },
  drawerTitle: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.display,
    fontSize: 28,
    marginBottom: 12,
  },
  drawerRow: {
    minHeight: 48,
    justifyContent: 'center',
  },
  drawerLabel: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  soon: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 13,
  },
  searchScreen: {
    flex: 1,
    backgroundColor: theme.colors.surfaceElevated,
    paddingHorizontal: 16,
    gap: 12,
  },
  searchInput: {
    height: 52,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    paddingHorizontal: 14,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  cancel: {
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  empty: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
  },
  emptyPage: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
    backgroundColor: theme.colors.bg,
    padding: 24,
  },
  news: {
    width: 96,
    height: 68,
  },
  emptyTitle: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 22,
  },
});
