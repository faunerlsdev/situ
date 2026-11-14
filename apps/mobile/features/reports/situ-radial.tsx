import { useRef, useState } from 'react';
import { AccessibilityInfo, Image, PanResponder, Pressable, StyleSheet, View } from 'react-native';

import { theme } from '@/theme/theme';

const isotipo = require('../../assets/images/brand/situ-isotipo-negro.png');
const transmitMark = require('../../assets/images/icons/nav-en-vivo-dark.png');
const reportMark = require('../../assets/images/icons/situ-pin-mapa-dark.png');

type Choice = 'report' | 'transmit';

/** Press the mark and slide, without lifting, to Transmitir (up) or Reportar (left). */
export function SituRadial({ onReport, onTransmit }: { onReport: () => void; onTransmit: () => void }) {
  const [open, setOpen] = useState(false);
  const [armed, setArmed] = useState<Choice | null>(null);
  const [reader, setReader] = useState(false);
  const armedRef = useRef<Choice | null>(null);
  const readerRef = useRef(false);
  const reportRef = useRef(onReport);
  const transmitRef = useRef(onTransmit);
  const origin = useRef({ x: 0, y: 0 });
  reportRef.current = onReport;
  transmitRef.current = onTransmit;
  readerRef.current = reader;

  function arm(next: Choice | null) {
    armedRef.current = next;
    setArmed(next);
  }

  const pan = useRef(
    PanResponder.create({
      onStartShouldSetPanResponderCapture: () => !readerRef.current,
      onMoveShouldSetPanResponderCapture: () => !readerRef.current,
      onPanResponderGrant: (event) => {
        origin.current = { x: event.nativeEvent.pageX, y: event.nativeEvent.pageY };
        setOpen(true);
        arm(null);
      },
      onPanResponderMove: (event) => {
        const dx = event.nativeEvent.pageX - origin.current.x;
        const dy = event.nativeEvent.pageY - origin.current.y;
        if (dy < -36 && Math.abs(dy) > Math.abs(dx)) {
          arm('transmit');
          return;
        }
        if (dx < -36 && Math.abs(dx) > Math.abs(dy)) {
          arm('report');
          return;
        }
        arm(null);
      },
      onPanResponderRelease: () => {
        const choice = armedRef.current;
        setOpen(false);
        arm(null);
        if (choice === 'transmit') {
          transmitRef.current();
        }
        if (choice === 'report') {
          reportRef.current();
        }
      },
      onPanResponderTerminate: () => {
        setOpen(false);
        arm(null);
      },
    }),
  ).current;

  function toggleReader() {
    setOpen((value) => !value);
  }

  return (
    <View style={styles.anchor} pointerEvents="box-none">
      {open ? (
        <>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Transmitir"
            accessibilityState={{ selected: armed === 'transmit' }}
            onPress={reader ? () => transmitRef.current() : undefined}
            style={[styles.choice, styles.up, armed === 'transmit' && styles.choiceOn]}
          >
            <Image source={transmitMark} resizeMode="contain" style={styles.transmitMark} />
          </Pressable>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Reportar"
            accessibilityState={{ selected: armed === 'report' }}
            onPress={reader ? () => reportRef.current() : undefined}
            style={[styles.choice, styles.left, armed === 'report' && styles.choiceOn]}
          >
            <Image source={reportMark} resizeMode="contain" style={styles.reportMark} />
          </Pressable>
        </>
      ) : null}
      <View
        {...(reader ? {} : pan.panHandlers)}
        accessibilityRole="button"
        accessibilityLabel="SITU. Mantén presionado y desliza hacia arriba para transmitir o hacia la izquierda para reportar."
        accessibilityHint="Desliza sin soltar"
        onAccessibilityTap={() => {
          void AccessibilityInfo.isScreenReaderEnabled().then((enabled) => {
            setReader(enabled);
            if (enabled) {
              toggleReader();
            }
          });
        }}
      >
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="SITU"
          onPress={() => {
            void AccessibilityInfo.isScreenReaderEnabled().then((enabled) => {
              if (!enabled) {
                return;
              }
              setReader(true);
              toggleReader();
            });
          }}
          style={styles.fab}
        >
          <Image source={isotipo} resizeMode="contain" style={styles.mark} />
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  anchor: {
    position: 'absolute',
    right: 16,
    bottom: 100,
    width: 148,
    height: 148,
    zIndex: 4,
    alignItems: 'flex-end',
    justifyContent: 'flex-end',
  },
  fab: {
    width: 64,
    height: 64,
    borderRadius: 32,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.surfaceElevated,
    borderWidth: 2,
    borderColor: theme.colors.primary,
  },
  mark: { width: 36, height: 46 },
  choice: {
    position: 'absolute',
    width: 56,
    height: 56,
    borderRadius: 28,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.surfaceElevated,
    borderWidth: 1,
    borderColor: theme.colors.border,
  },
  choiceOn: { borderWidth: 2, borderColor: theme.colors.primary },
  up: { bottom: 76, right: 4 },
  left: { bottom: 4, right: 76 },
  transmitMark: { width: 44, height: 20 },
  reportMark: { width: 28, height: 28 },
});
