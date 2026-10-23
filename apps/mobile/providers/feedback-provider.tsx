import {
  AppError,
  bindNotify,
  createDialogStore,
  createToastQueue,
  feedbackMessages,
  notify,
  sessionEvents,
} from '@situ/shared';
import { useRouter } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '@/theme/theme';

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queue = useMemo(() => createToastQueue(), []);
  const dialogs = useMemo(() => createDialogStore(), []);
  const [toast, setToast] = useState(queue.current());
  const [dialog, setDialog] = useState(dialogs.current());
  const [offline, setOffline] = useState(false);

  useEffect(() => {
    bindNotify({
      success: (message, options) => queue.enqueue({ kind: 'success', message, action: options?.action }),
      error: (message, options) => queue.enqueue({ kind: 'error', message, action: options?.action }),
      warning: (message, options) => queue.enqueue({ kind: 'warning', message, action: options?.action }),
      info: (message, options) => queue.enqueue({ kind: 'info', message, action: options?.action }),
      brand: (message) => queue.enqueue({ kind: 'brand', message }),
      dismiss: (id) => queue.dismiss(id),
      confirm: (input) => dialogs.confirm(input),
      alertModal: (input) => dialogs.alert(input),
    });
    return () => bindNotify(null);
  }, [dialogs, queue]);

  useEffect(() => queue.subscribe(() => setToast(queue.current())), [queue]);
  useEffect(() => dialogs.subscribe(() => setDialog(dialogs.current())), [dialogs]);
  useEffect(() => sessionEvents.subscribe(() => {
    void notify;
    dialogs.alert({
      title: feedbackMessages.sessionExpiredTitle,
      message: feedbackMessages.sessionExpiredBody,
      type: 'warning',
      buttonText: feedbackMessages.understood,
      required: true,
    }).then(() => {
      sessionEvents.reset();
      router.replace('/login');
    });
  }), [dialogs, router]);

  useEffect(() => {
    let unsubscribe: () => void = () => undefined;
    import('@react-native-community/netinfo')
      .then((NetInfo) => {
        unsubscribe = NetInfo.addEventListener((state) => {
          const connected = state.isConnected !== false;
          setOffline((was) => {
            if (was && connected) {
              notify.success(feedbackMessages.online);
            }
            return !connected;
          });
        });
      })
      .catch(() => undefined);
    return () => unsubscribe();
  }, []);

  return (
    <View style={styles.fill}>
      {children}
      {offline ? (
        <Text style={[styles.offline, { top: insets.top + 56 }]}>{feedbackMessages.offline}</Text>
      ) : null}
      {toast ? (
        <Pressable
          accessibilityRole="alert"
          onPress={() => queue.dismiss(toast.id)}
          style={[styles.toast, { top: insets.top + 72 }, toast.kind === 'info' ? styles.toastInfo : null]}
        >
          <Text style={[styles.toastText, toast.kind === 'info' ? styles.toastTextLight : null]} numberOfLines={2}>
            {toast.message}
          </Text>
        </Pressable>
      ) : null}
      <Modal visible={dialog !== null} transparent animationType="fade" onRequestClose={() => {
        if (dialog && !dialog.request.required) {
          dialogs.answer(false);
        }
      }}>
        <View style={styles.backdrop} accessibilityViewIsModal>
          <View style={styles.card}>
            <Text style={styles.title}>{dialog?.request.title}</Text>
            <Text style={styles.body}>{dialog?.request.message}</Text>
            <Pressable accessibilityRole="button" style={styles.primary} onPress={() => dialogs.answer(true)}>
              <Text style={styles.primaryText}>
                {dialog?.kind === 'confirm' ? (dialog.request.confirmText ?? feedbackMessages.understood) : (dialog?.request.buttonText ?? feedbackMessages.understood)}
              </Text>
            </Pressable>
            {dialog?.kind === 'confirm' ? (
              <Pressable accessibilityRole="button" style={styles.secondary} onPress={() => dialogs.answer(false)}>
                <Text style={styles.secondaryText}>{dialog.request.cancelText ?? feedbackMessages.cancel}</Text>
              </Pressable>
            ) : null}
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  offline: {
    position: 'absolute',
    left: 0,
    right: 0,
    zIndex: 70,
    backgroundColor: theme.colors.bgDarkEdge,
    color: theme.colors.white,
    textAlign: 'center',
    paddingVertical: 8,
    fontFamily: theme.fontFamily.sansBold,
  },
  toast: {
    position: 'absolute',
    left: 16,
    right: 16,
    zIndex: 80,
    borderLeftWidth: 4,
    borderLeftColor: theme.colors.live,
    borderRadius: 12,
    backgroundColor: theme.colors.white,
    padding: 12,
    boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
  },
  toastInfo: { backgroundColor: theme.colors.bgDarkEdge, borderLeftColor: theme.colors.bgDarkEdge },
  toastText: { color: theme.colors.ink, fontFamily: theme.fontFamily.sansBold, fontSize: 13 },
  toastTextLight: { color: theme.colors.white },
  backdrop: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.55)', padding: 24 },
  card: { width: '100%', maxWidth: 340, borderRadius: 20, backgroundColor: theme.colors.white, padding: 24 },
  title: { fontFamily: theme.fontFamily.display, fontSize: 20, color: theme.colors.ink, textAlign: 'center' },
  body: { marginTop: 8, fontFamily: theme.fontFamily.sans, fontSize: 15, color: theme.colors.ink, textAlign: 'center' },
  primary: { marginTop: 20, height: 48, borderRadius: 4, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.primary },
  primaryText: { color: theme.colors.white, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  secondary: { marginTop: 8, height: 48, borderRadius: 4, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: theme.colors.ink },
  secondaryText: { color: theme.colors.ink, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});

void AppError;
