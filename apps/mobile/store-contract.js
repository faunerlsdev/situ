/** Store permission contract. app.config.js and the config test both read this file. */

const APP_ID = 'com.somossitu.app';

const locationWhenInUse =
  'SITU usa tu ubicación mientras la app está abierta para centrar el mapa, calcular distancias, confirmar que estás cerca de un reporte y, si tú lo activas, compartir un punto aproximado con tus amigos o en el mapa general. No la usamos en segundo plano.';

const camera =
  'SITU usa la cámara para que tomes fotos o videos de tus reportes y para transmitir en vivo.';

const microphone =
  'SITU usa el micrófono para grabar el audio de tus videos y de tus transmisiones en vivo.';

const faceId = 'SITU usa Face ID para desbloquear tu sesión en este dispositivo.';

const collected = (type) => ({
  NSPrivacyCollectedDataType: type,
  NSPrivacyCollectedDataTypeLinked: true,
  NSPrivacyCollectedDataTypeTracking: false,
  NSPrivacyCollectedDataTypePurposes: ['NSPrivacyCollectedDataTypePurposeAppFunctionality'],
});

const privacyManifests = {
  NSPrivacyTracking: false,
  NSPrivacyCollectedDataTypes: [
    collected('NSPrivacyCollectedDataTypePreciseLocation'),
    collected('NSPrivacyCollectedDataTypeCoarseLocation'),
    collected('NSPrivacyCollectedDataTypeName'),
    collected('NSPrivacyCollectedDataTypeEmailAddress'),
    collected('NSPrivacyCollectedDataTypePhoneNumber'),
    collected('NSPrivacyCollectedDataTypePhotosorVideos'),
    collected('NSPrivacyCollectedDataTypeUserID'),
    collected('NSPrivacyCollectedDataTypeOtherUserContent'),
    collected('NSPrivacyCollectedDataTypeProductInteraction'),
  ],
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
  camera,
  microphone,
  faceId,
  iosUsage: {
    NSLocationWhenInUseUsageDescription: locationWhenInUse,
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
