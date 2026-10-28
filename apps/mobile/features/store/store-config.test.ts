import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const mobileRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(import.meta.url);
const contract = require(path.join(mobileRoot, 'store-contract.js')) as {
  appId: string;
  iosUsage: Record<string, string>;
  androidPermissions: string[];
  blockedPermissions: string[];
  privacyManifests: { NSPrivacyTracking: boolean; NSPrivacyAccessedAPITypes: unknown[] };
};

const envKeys = [
  'EXPO_PUBLIC_DOMAIN_ROOT',
  'DOMAIN_ROOT',
  'API_BASE_URL',
  'EXPO_PUBLIC_API_URL',
  'EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_ANDROID',
  'EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_IOS',
  'EXPO_PUBLIC_LIVE_POC',
];

type ExpoConfig = {
  ios: {
    bundleIdentifier: string;
    associatedDomains?: string[];
    infoPlist: Record<string, unknown>;
    privacyManifests: { NSPrivacyTracking: boolean; NSPrivacyAccessedAPITypes: unknown[] };
  };
  android: {
    package: string;
    permissions: string[];
    blockedPermissions: string[];
    usesCleartextTraffic?: boolean;
    intentFilters?: { autoVerify?: boolean; data?: { host?: string; pathPrefix?: string }[] }[];
  };
  updates?: { url?: string };
  slug?: string;
};

function loadExpoConfig(env: Record<string, string>) {
  const previous = Object.fromEntries(envKeys.map((key) => [key, process.env[key]]));
  for (const key of envKeys) {
    delete process.env[key];
  }
  Object.assign(process.env, env);
  const configPath = path.join(mobileRoot, 'app.config.js');
  delete require.cache[configPath];
  try {
    const loadedModule = require(configPath) as
      | { expo: ExpoConfig }
      | ((input: { config: ExpoConfig }) => { expo?: ExpoConfig } | ExpoConfig);
    const appJson = require(path.join(mobileRoot, 'app.json')) as { expo: ExpoConfig };
    const loaded = typeof loadedModule === 'function' ? loadedModule({ config: appJson.expo }) : loadedModule;
    const expoConfig = 'expo' in loaded && loaded.expo ? loaded.expo : loaded;
    return {
      apiUrl: process.env.EXPO_PUBLIC_API_URL,
      domain: process.env.EXPO_PUBLIC_DOMAIN_ROOT,
      expo: expoConfig as ExpoConfig,
    };
  } finally {
    for (const key of envKeys) {
      if (previous[key] === undefined) {
        delete process.env[key];
      } else {
        process.env[key] = previous[key];
      }
    }
    delete require.cache[configPath];
  }
}

describe('store config', () => {
  it('keeps the same id on iOS and Android and only the permissions this app uses', () => {
    const { expo } = loadExpoConfig({});
    expect(expo.ios.bundleIdentifier).toBe(contract.appId);
    expect(expo.android.package).toBe(contract.appId);
    expect(expo.ios.bundleIdentifier).toBe('com.somossitu.app');
    expect(expo.android.permissions).toEqual(contract.androidPermissions);
    expect(expo.android.blockedPermissions).toEqual(contract.blockedPermissions);
    expect(expo.android.blockedPermissions).toContain('android.permission.ACCESS_BACKGROUND_LOCATION');
    expect(expo.android.usesCleartextTraffic).toBeUndefined();
    expect(expo.android.blockedPermissions).not.toContain('android.permission.CAMERA');
    expect(expo.android.blockedPermissions).not.toContain('android.permission.RECORD_AUDIO');
    expect(expo.android.blockedPermissions).not.toContain('android.permission.ACCESS_FINE_LOCATION');

    for (const [key, text] of Object.entries(contract.iosUsage)) {
      expect(text.length).toBeGreaterThan(20);
      expect(text).toContain('SITU');
      expect(expo.ios.infoPlist[key]).toBe(text);
    }

    expect(expo.ios.infoPlist.NSLocationAlwaysAndWhenInUseUsageDescription).toBeUndefined();
    expect(expo.ios.infoPlist.NSLocationAlwaysUsageDescription).toBeUndefined();
    expect(expo.ios.infoPlist.NSUserTrackingUsageDescription).toBeUndefined();
    expect(expo.ios.privacyManifests.NSPrivacyTracking).toBe(false);
    expect(expo.ios.privacyManifests.NSPrivacyAccessedAPITypes.length).toBeGreaterThan(0);
    expect(expo.updates?.url).toBeUndefined();
  });

  it('points universal links and the API at the configured domain', () => {
    const { expo, apiUrl, domain } = loadExpoConfig({ DOMAIN_ROOT: 'example.test' });
    expect(domain).toBe('example.test');
    expect(apiUrl).toBe('https://api.example.test');
    expect(expo.ios.associatedDomains).toEqual(['applinks:app.example.test']);
    expect(expo.android.intentFilters?.[0]?.autoVerify).toBe(true);
    expect(expo.android.intentFilters?.[0]?.data?.[0]).toMatchObject({
      host: 'app.example.test',
      pathPrefix: '/r/',
    });
  });

  it('allows cleartext only for the local live proof', () => {
    const enabled = loadExpoConfig({ EXPO_PUBLIC_LIVE_POC: '1' });
    expect(enabled.expo.android.usesCleartextTraffic).toBe(true);
  });

  it('reads API_BASE_URL when the public API variable is absent', () => {
    const loaded = loadExpoConfig({ API_BASE_URL: 'https://api.example.test' });
    expect(loaded.apiUrl).toBe('https://api.example.test');
  });
});

describe('eas profiles', () => {
  const eas = JSON.parse(readFileSync(path.join(mobileRoot, 'eas.json'), 'utf8')) as {
    cli: { appVersionSource: string };
    build: Record<string, { distribution?: string; autoIncrement?: boolean; developmentClient?: boolean; channel?: string }>;
    submit: { production: { android: { track: string; serviceAccountKeyPath: string } } };
  };
  const mobilePackage = JSON.parse(readFileSync(path.join(mobileRoot, 'package.json'), 'utf8')) as {
    dependencies: Record<string, string>;
  };

  it('defines internal preview, store production, and no over-the-air updates', () => {
    expect(eas.cli.appVersionSource).toBe('remote');
    expect(eas.build.development?.developmentClient).toBe(true);
    expect(eas.build.development?.distribution).toBe('internal');
    expect(eas.build.preview?.distribution).toBe('internal');
    expect(eas.build.preview?.autoIncrement).toBe(true);
    expect(eas.build.production?.distribution).toBe('store');
    expect(eas.build.production?.autoIncrement).toBe(true);
    expect(eas.build.production?.channel).toBeUndefined();
    expect(eas.submit.production.android.track).toBe('internal');
    expect(eas.submit.production.android.serviceAccountKeyPath).toBe('$GOOGLE_SERVICE_ACCOUNT_KEY_PATH');
    expect(mobilePackage.dependencies['expo-updates']).toBeUndefined();
  });
});
