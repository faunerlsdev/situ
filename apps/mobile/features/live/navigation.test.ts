import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const mobileRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function source(relative: string): string {
  return readFileSync(path.join(mobileRoot, relative), 'utf8');
}

function walk(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const full = path.join(directory, entry);
    if (statSync(full).isDirectory()) {
      return walk(full);
    }
    return full.endsWith('.ts') || full.endsWith('.tsx') ? [full] : [];
  });
}

describe('live navigation', () => {
  it('opens the live list from the toolbar and keeps transmit off the map fabs', () => {
    const map = source('features/reports/map-screen.tsx');
    const header = source('components/shell.tsx');
    const mark = source('features/live/toolbar-mark.tsx');
    const tabs = source('app/(tabs)/_layout.tsx');
    const root = source('app/_layout.tsx');
    expect(map).not.toContain('accessibilityLabel="En vivo ahora"');
    expect(map).not.toContain('liveFab');
    expect(map).not.toContain('Transmitir en vivo');
    expect(map).not.toContain('TransmitLiveControl');
    expect(header).toContain('LiveToolbarMark');
    expect(header).not.toContain('TransmitLiveControl');
    expect(header).not.toContain('nav-en-vivo');
    expect(header).toContain('nav-chisme-megafono-dark.png');
    expect(header).toContain("icon: chismeIcon");
    expect(header).toContain("router.push('/en-vivo')");
    expect(source('app/en-vivo/iniciar.tsx')).toContain('Compartir ubicación');
    expect(source('app/en-vivo/iniciar.tsx')).toContain('liveShareNote');
    expect(source('app/en-vivo/iniciar.tsx')).toContain('useState(false)');
    expect(mark).toContain('Ver transmisiones en vivo');
    expect(source('app/en-vivo/index.tsx')).toContain('label="Transmitir"');
    expect(source('features/live/transmit-control.tsx')).toContain('liveStartState');
    expect(tabs).not.toContain('en-vivo');
    expect(root).toContain('<Stack.Screen name="en-vivo" />');
    expect(source('app/en-vivo/iniciar.tsx')).toContain('ANONYMITY_REMINDER');
    expect(source('features/live/logic.ts')).toContain('Tu transmisión es anónima, pero el video puede mostrar rostros y lugares');
  });

  it('uses the publisher double when the native module is not the resolved file', () => {
    expect(source('features/live/publisher.tsx')).toContain('return false');
    expect(source('features/live/publisher.native.tsx')).toContain('const loaded = loadPublisher()');
    expect(source('features/live/publisher.native.tsx')).not.toContain("Platform.OS === 'android' ? loadPublisher()");
    expect(source('features/live/logic.ts')).not.toContain("platform === 'ios'");
    expect(source('features/live/publisher.native.tsx')).toContain('nativePublishUrl');
    expect(source('features/live/publisher.native.tsx')).not.toContain('setAuthorization');
    expect(source('app/en-vivo/transmitir.tsx')).toContain('shouldEndInBackground');
    expect(source('app/en-vivo/[id].tsx')).toContain('renewalDelayMs');
    expect(source('features/notifications/logic.ts')).toContain('FriendLiveStarted');
    expect(source('features/notifications/push-runtime.tsx')).toContain("pathname: '/en-vivo/[id]'");
  });

  it('does not log publish keys or playback urls', () => {
    const files = walk(path.join(mobileRoot, 'features/live')).concat(walk(path.join(mobileRoot, 'app/en-vivo')));
    const logged = files.filter((file) => /console\.(log|debug|info|warn|error)/.test(readFileSync(file, 'utf8')));
    expect(logged.map((file) => path.relative(mobileRoot, file))).toEqual(['features/live/debug.ts']);
    const debug = source('features/live/debug.ts');
    expect(debug).toContain('if (!__DEV__)');
    expect(debug).toContain('scrubLiveDetail');
  });
});
