import { useRouter, usePathname } from 'expo-router';
import { createContext, useContext, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Image, Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

const wordmark = require('../assets/images/brand/situ-wordmark-naranja.png');
const searchIcon = require('../assets/images/icons/ic-buscar.png');
const newsIcon = require('../assets/images/icons/situ-noticias.png');
const mapIcon = require('../assets/images/icons/nav-mapa-globo.png');
const chismeIcon = require('../assets/images/icons/nav-chisme-megafono.png');
const liveIcon = require('../assets/images/icons/nav-en-vivo.png');
const messagesIcon = require('../assets/images/icons/nav-mensajes.png');

type ShellContextValue = {
  openDrawer: () => void;
  openSearch: () => void;
};

const ShellContext = createContext<ShellContextValue | null>(null);

const tabs = [
  { href: '/mapa', label: 'Mapa', icon: mapIcon },
  { href: '/chisme', label: 'Chisme', icon: chismeIcon },
  { href: '/notificaciones', label: 'En vivo', icon: liveIcon },
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

  return (
    <View style={{ backgroundColor: theme.colors.surface, paddingBottom: insets.bottom }}>
      <View style={styles.divider} />
      <View style={styles.tabs}>
        {tabs.map((tab) => {
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
              <Image source={tab.icon} resizeMode="contain" style={[styles.tabIcon, !active && styles.inactive]} />
              {active ? <View style={styles.dot} /> : <View style={styles.dotSpacer} />}
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

function AccountDrawer({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const router = useRouter();
  const { signOut } = useSession();
  const insets = useSafeAreaInsets();

  function go(href: '/perfil' | '/amigos') {
    onClose();
    router.push(href);
  }

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.modalFill}>
        <Pressable accessibilityLabel="Cerrar menú" style={styles.backdrop} onPress={onClose} />
        <View style={[styles.drawer, { paddingTop: insets.top + 24 }, theme.shadowSheet]}>
          <Text style={styles.drawerTitle}>Mi SITU</Text>
          <Pressable accessibilityRole="button" onPress={() => go('/perfil')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Perfil</Text>
          </Pressable>
          <Pressable accessibilityRole="button" onPress={() => go('/amigos')} style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Amigos</Text>
          </Pressable>
          <View style={styles.drawerRow}>
            <Text style={styles.drawerLabel}>Ajustes</Text>
            <Text style={styles.soon}>Próximamente</Text>
          </View>
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

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={[styles.searchScreen, { paddingTop: insets.top + 12 }]}>
        <TextInput
          autoFocus
          placeholder="Buscar"
          placeholderTextColor={theme.colors.gray400}
          accessibilityLabel="Buscar"
          style={styles.searchInput}
        />
        <Pressable accessibilityRole="button" onPress={onClose}>
          <Text style={styles.cancel}>Cerrar</Text>
        </Pressable>
        <View style={styles.empty}>
          <Image source={newsIcon} resizeMode="contain" style={styles.news} />
          <Text style={styles.soon}>Próximamente</Text>
        </View>
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
    backgroundColor: theme.colors.gray200,
  },
  avatarText: {
    color: theme.colors.ink,
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
    opacity: 0.55,
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
  modalFill: {
    flex: 1,
    flexDirection: 'row',
  },
  backdrop: {
    ...StyleSheet.absoluteFill,
    backgroundColor: theme.colors.black,
    opacity: 0.4,
  },
  drawer: {
    width: 280,
    height: '100%',
    backgroundColor: theme.colors.surface,
    paddingHorizontal: 20,
    gap: 8,
  },
  drawerTitle: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.display,
    fontSize: 28,
    marginBottom: 12,
  },
  drawerRow: {
    minHeight: 48,
    justifyContent: 'center',
  },
  drawerLabel: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  soon: {
    color: theme.colors.gray600,
    fontFamily: theme.fontFamily.sans,
    fontSize: 13,
  },
  searchScreen: {
    flex: 1,
    backgroundColor: theme.colors.surface,
    paddingHorizontal: 16,
    gap: 12,
  },
  searchInput: {
    height: 52,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.gray200,
    paddingHorizontal: 14,
    color: theme.colors.ink,
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
    backgroundColor: theme.colors.surface,
    padding: 24,
  },
  news: {
    width: 96,
    height: 68,
  },
  emptyTitle: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 22,
  },
});
