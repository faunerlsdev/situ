/** Store permission contract. app.config.js and the config test both read this file. */

const APP_ID = 'com.somossitu.app';

const locationWhenInUse =
  'SITU usa tu ubicación mientras la app está abierta para centrar el mapa, calcular distancias, confirmar que estás cerca de un reporte y, si tú lo activas, compartir un punto aproximado con tus amigos o en el mapa general. No la usamos en segundo plano.';

const photos =
  'SITU accede a tus fotos y videos solo cuando eliges uno para publicarlo en un reporte o en tu foto de perfil.';

const camera =
  'SITU usa la cámara cuando tú tomas una foto o grabas un video para un reporte, o cuando tú inicias una transmisión en vivo.';

const microphone =
  'SITU usa el micrófono cuando grabas un video para un reporte o cuando tú inicias una transmisión en vivo. No graba audio por su cuenta.';

const faceId = 'SITU usa Face ID para desbloquear tu sesión en este dispositivo.';

const privacyManifests = {
  NSPrivacyTracking: false,
  NSPrivacyCollectedDataTypes: [],
  NSPrivacyAccessedAPITypes: [
    {
      NSPrivacyAccessedAPIType: 'NSPrivacyAccessedAPICategoryUserDefaults',
      NSPrivacyAccessedAPITypeReasons: ['CA92.1'],
    },
    {
      NSPrivacyAccessedAPIType: 'NSPrivacyAccessedAPICategoryFileTimestamp',
      NSPrivacyAccessedAPITypeReasons: ['C617.1'],
    },
    {
      NSPrivacyAccessedAPIType: 'NSPrivacyAccessedAPICategoryDiskSpace',
      NSPrivacyAccessedAPITypeReasons: ['E174.1'],
    },
    {
      NSPrivacyAccessedAPIType: 'NSPrivacyAccessedAPICategorySystemBootTime',
      NSPrivacyAccessedAPITypeReasons: ['35F9.1'],
    },
  ],
};

module.exports = {
  appId: APP_ID,
  locationWhenInUse,
  photos,
  camera,
  microphone,
  faceId,
  iosUsage: {
    NSLocationWhenInUseUsageDescription: locationWhenInUse,
    NSPhotoLibraryUsageDescription: photos,
    NSCameraUsageDescription: camera,
    NSMicrophoneUsageDescription: microphone,
    NSFaceIDUsageDescription: faceId,
  },
  androidPermissions: [
    'android.permission.ACCESS_COARSE_LOCATION',
    'android.permission.ACCESS_FINE_LOCATION',
    'android.permission.CAMERA',
    'android.permission.RECORD_AUDIO',
    'android.permission.WAKE_LOCK',
    'android.permission.POST_NOTIFICATIONS',
    'android.permission.USE_BIOMETRIC',
    'android.permission.USE_FINGERPRINT',
  ],
  blockedPermissions: [
    'android.permission.ACCESS_BACKGROUND_LOCATION',
    'android.permission.ACCESS_MEDIA_LOCATION',
    'android.permission.ACTIVITY_RECOGNITION',
    'android.permission.FOREGROUND_SERVICE_LOCATION',
    'android.permission.READ_EXTERNAL_STORAGE',
    'android.permission.WRITE_EXTERNAL_STORAGE',
    'android.permission.READ_MEDIA_IMAGES',
    'android.permission.READ_MEDIA_VIDEO',
    'android.permission.READ_MEDIA_AUDIO',
    'android.permission.READ_MEDIA_VISUAL_USER_SELECTED',
    'android.permission.SYSTEM_ALERT_WINDOW',
    'com.google.android.gms.permission.AD_ID',
  ],
  privacyManifests,
};
