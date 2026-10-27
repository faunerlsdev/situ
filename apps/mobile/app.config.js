const fs = require('fs');
const path = require('path');

const contract = require('./store-contract');
const { applyStoreEnv } = require('./public-env');

const APP_ID = contract.appId;
const domainRoot = applyStoreEnv();
const EAS_PROJECT_ID = '947392db-a743-40ed-a7b2-fb7e54206c44';
const EAS_OWNER = 'faunerls.dev';
const EAS_SLUG = 'situ';

if (!/^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$/.test(APP_ID)) {
  throw new Error('APP_ID must use lowercase segments separated by dots, each starting with a letter.');
}

const appJson = require('./app.json');

const androidMapsKey = process.env.EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_ANDROID?.trim();
const iosMapsKey = process.env.EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_IOS?.trim();
const expo = appJson.expo;

if (expo.slug !== EAS_SLUG) {
  throw new Error(`Expo slug must be "${EAS_SLUG}" so it matches the EAS project @${EAS_OWNER}/${EAS_SLUG}.`);
}

const easProjectId = process.env.EAS_PROJECT_ID?.trim() || EAS_PROJECT_ID;
const googleServicesFile = resolveGoogleServicesFile(process.env.GOOGLE_SERVICES_JSON);

function resolveGoogleServicesFile(configuredPath) {
  const trimmed = configuredPath?.trim();
  if (!trimmed) {
    warnMissingGoogleServices('GOOGLE_SERVICES_JSON no está definida.');
    return undefined;
  }

  const resolved = path.isAbsolute(trimmed) ? trimmed : path.resolve(__dirname, trimmed);
  if (!fs.existsSync(resolved)) {
    warnMissingGoogleServices('GOOGLE_SERVICES_JSON no apunta a un archivo existente.');
    return undefined;
  }

  return resolved;
}

function warnMissingGoogleServices(message) {
  if (process.env.EAS_BUILD === 'true' || process.env.NODE_ENV === 'production') {
    return;
  }

  console.warn(`[SITU] ${message} Android arranca sin notificaciones push.`);
}

/** Las keys no van en el código. iOS sin key usa Apple Maps. Android sin key no muestra el mapa. */
module.exports = ({ config } = {}) => ({
  ...(config?.slug ? config : expo),
    owner: EAS_OWNER,
    slug: EAS_SLUG,
    extra: {
      ...expo.extra,
      eas: {
        ...expo.extra?.eas,
        projectId: easProjectId,
      },
    },
    plugins: [
      ...expo.plugins,
      'expo-notifications',
      [
        'expo-location',
        {
          locationWhenInUsePermission: contract.locationWhenInUse,
          isIosBackgroundLocationEnabled: false,
          isAndroidBackgroundLocationEnabled: false,
        },
      ],
      [
        'expo-image-picker',
        {
          photosPermission: contract.photos,
          cameraPermission: contract.camera,
          microphonePermission: contract.microphone,
        },
      ],
    ],
    ios: {
      ...expo.ios,
      bundleIdentifier: APP_ID,
      ...(domainRoot ? { associatedDomains: [`applinks:app.${domainRoot}`] } : {}),
      infoPlist: {
        ...expo.ios.infoPlist,
        ...contract.iosUsage,
        ITSAppUsesNonExemptEncryption: false,
      },
      privacyManifests: contract.privacyManifests,
      config: {
        ...expo.ios.config,
        usesNonExemptEncryption: false,
        ...(iosMapsKey ? { googleMapsApiKey: iosMapsKey } : {}),
      },
    },
    android: {
      ...expo.android,
      package: APP_ID,
      permissions: contract.androidPermissions,
      blockedPermissions: contract.blockedPermissions,
      ...(domainRoot
        ? {
            intentFilters: [
              {
                action: 'VIEW',
                autoVerify: true,
                data: [{ scheme: 'https', host: `app.${domainRoot}`, pathPrefix: '/r/' }],
                category: ['BROWSABLE', 'DEFAULT'],
              },
            ],
          }
        : {}),
      ...(androidMapsKey ? { config: { ...expo.android.config, googleMaps: { apiKey: androidMapsKey } } } : {}),
      ...(googleServicesFile ? { googleServicesFile } : {}),
    },
});
