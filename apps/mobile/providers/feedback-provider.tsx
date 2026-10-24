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
import { Dimensions, Image, Keyboard, Modal, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { FullWindowOverlay } from 'react-native-screens';

import { theme } from '@/theme/theme';

const isotipo = require('../assets/images/brand/situ-isotipo-naranja-claro.png');
const isotipoOnPrimary = require('../assets/images/brand/situ-isotipo-negro.png');

const toastAccent = {
  success: theme.colors.success,
  error: theme.colors.danger,
  warning: theme.colors.warning,
  info: theme.colors.primary,
  brand: theme.colors.primary,
} as const;

const toastTitle = {
  success: 'Listo',
  error: 'No se pudo',
  warning: 'Revisa esto',
  info: 'Aviso',
  brand: 'SITU',
} as const;

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queue = useMemo(() => createToastQueue(), []);
  const dialogs = useMemo(() => createDialogStore(), []);
  const [toast, setToast] = useState(queue.current());
  const [dialog, setDialog] = useState(dialogs.current());
  const [offline, setOffline] = useState(false);
  const keyboardOverlap = useKeyboardOverlap();

  useEffect(() => {
    bindNotify({
      success: (message, options) => queue.enqueue({ kind: 'success', message, action: options?.action, placement: options?.placement }),
      error: (message, options) => queue.enqueue({ kind: 'error', message, action: options?.action, placement: options?.placement }),
      warning: (message, options) => queue.enqueue({ kind: 'warning', message, action: options?.action, placement: options?.placement }),
      info: (message, options) => queue.enqueue({ kind: 'info', message, action: options?.action, placement: options?.placement }),
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
        <ToastOverlay>
          <View
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
            style={[
              styles.toast,
              keyboardOverlap > 0 || toast.placement === 'bottom'
                ? { bottom: (keyboardOverlap > 0 ? keyboardOverlap : insets.bottom) + 12 }
                : { top: insets.top + 12 },
              toast.kind === 'success' ? styles.toastSuccess : null,
              toast.kind === 'error' ? styles.toastDanger : null,
              toast.kind !== 'success' && toast.kind !== 'error' ? { borderLeftColor: toastAccent[toast.kind] } : null,
            ]}
          >
            <Pressable accessibilityRole="button" onPress={() => queue.dismiss(toast.id)} style={styles.toastMain}>
              <Image source={toast.kind === 'success' ? isotipoOnPrimary : isotipo} resizeMode="contain" style={styles.toastMark} />
              <View style={styles.toastCopy}>
                <Text style={[styles.toastTitle, toast.kind === 'success' && styles.toastOnSuccess, toast.kind === 'error' && styles.toastOnDanger]}>{toastTitle[toast.kind]}</Text>
                <Text style={[styles.toastText, toast.kind === 'success' && styles.toastOnSuccess, toast.kind === 'error' && styles.toastOnDanger]}>{toast.message}</Text>
              </View>
            </Pressable>
            {toast.action ? (
              <Pressable accessibilityRole="button" onPress={toast.action.onPress}>
                <Text style={[styles.toastAction, toast.kind === 'success' && styles.toastOnSuccess, toast.kind === 'error' && styles.toastOnDanger]}>{toast.action.label}</Text>
              </Pressable>
            ) : null}
          </View>
        </ToastOverlay>
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
            <Pressable
              accessibilityRole="button"
              style={[styles.primary, dialog?.kind === 'confirm' && dialog.request.type === 'danger' && styles.danger]}
              onPress={() => dialogs.answer(true)}
            >
              <Text style={[styles.primaryText, dialog?.kind === 'confirm' && dialog.request.type === 'danger' && styles.dangerText]}>
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

function useKeyboardOverlap() {
  const [overlap, setOverlap] = useState(0);

  useEffect(() => {
    const showEvent = Platform.OS === 'ios' ? 'keyboardWillChangeFrame' : 'keyboardDidShow';
    const hideEvent = Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide';
    const show = Keyboard.addListener(showEvent, (event) => {
      const screenHeight = Dimensions.get('screen').height;
      setOverlap(Math.max(0, screenHeight - event.endCoordinates.screenY));
    });
    const hide = Keyboard.addListener(hideEvent, () => setOverlap(0));
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);

  return overlap;
}

function ToastOverlay({ children }: { children: ReactNode }) {
  if (Platform.OS === 'ios') {
    return (
      <FullWindowOverlay>
        <View pointerEvents="box-none" style={StyleSheet.absoluteFill}>
          {children}
        </View>
      </FullWindowOverlay>
    );
  }

  return children;
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  offline: {
    position: 'absolute',
    left: 0,
    right: 0,
    zIndex: 70,
    backgroundColor: theme.colors.surfaceElevated,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.warning,
    color: theme.colors.textPrimary,
    textAlign: 'center',
    paddingVertical: 8,
    fontFamily: theme.fontFamily.sansBold,
  },
  toast: {
    position: 'absolute',
    left: 16,
    right: 16,
    zIndex: 80,
    elevation: 24,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderLeftWidth: 4,
    borderRadius: 16,
    backgroundColor: theme.colors.surfaceElevated,
    paddingHorizontal: 14,
    paddingVertical: 14,
    ...theme.shadow,
  },
  toastMain: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 12 },
  toastMark: { width: 22, height: 28 },
  toastCopy: { flex: 1, gap: 2 },
  toastTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 15 },
  toastText: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 20 },
  toastSuccess: {
    borderLeftWidth: 0,
    backgroundColor: theme.colors.primary,
  },
  toastDanger: {
    borderLeftWidth: 0,
    backgroundColor: theme.colors.dangerSolid,
  },
  toastOnSuccess: { color: theme.colors.textOnPrimary },
  toastOnDanger: { color: theme.colors.textOnDanger },
  toastAction: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  backdrop: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.overlay, padding: 24 },
  card: { width: '100%', maxWidth: 340, borderRadius: 20, backgroundColor: theme.colors.surfaceElevated, padding: 24 },
  title: { fontFamily: theme.fontFamily.display, fontSize: 20, color: theme.colors.textPrimary, textAlign: 'center' },
  body: { marginTop: 8, fontFamily: theme.fontFamily.sans, fontSize: 15, color: theme.colors.textSecondary, textAlign: 'center' },
  primary: { marginTop: 20, height: 48, borderRadius: theme.radius.button, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.primary },
  danger: { backgroundColor: theme.colors.dangerSolid },
  primaryText: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  dangerText: { color: theme.colors.textOnDanger },
  secondary: { marginTop: 8, height: 48, borderRadius: theme.radius.button, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: theme.colors.border },
  secondaryText: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});

void AppError;
