import { loginErrorMessages } from '../feedback/messages';
import type { AppErrorCode } from './errors';

export const errorMessages: Record<AppErrorCode, string> = {
  NETWORK: 'Sin conexión. Revisa tu internet e inténtalo de nuevo.',
  TIMEOUT: 'La solicitud tardó demasiado. Inténtalo de nuevo.',
  UNAUTHORIZED: 'Tu sesión ya no es válida. Vuelve a ingresar.',
  FORBIDDEN: 'No puedes hacer esto con tu cuenta.',
  NOT_FOUND: 'No encontramos lo que buscabas.',
  VALIDATION: 'Revisa los datos e inténtalo de nuevo.',
  CONFLICT: 'Esa información ya está en uso.',
  RATE_LIMIT: 'Demasiados intentos. Espera un momento.',
  SERVER: 'Tuvimos un problema. Inténtalo de nuevo en un momento.',
  CANCELLED: '',
  UNKNOWN: 'No pudimos completar la solicitud.',
};

/** Curated product copy. A ProblemDetails detail is shown when it is a sentence for the user. */
export const apiMessages: Record<string, string> = {
  ...loginErrorMessages,
  'guardian_consent.email_rejected': 'Escribe el correo de tu representante. Debe ser distinto al tuyo.',
  'guardian_consent.already_open': 'Ya hay una solicitud esperando respuesta.',
  'guardian_consent.declined': 'Tu representante no autorizó la cuenta.',
  'guardian_consent.notice_required': 'Antes de enviar la solicitud, lee la explicación y marca Entendido.',
  'guardian_consent.send_limited': 'Llegaste al límite de envíos. Podrás intentarlo de nuevo más tarde.',
  'guardian_consent.code_invalid': 'El código no es válido. Revisa el correo e inténtalo de nuevo.',
  'guardian_consent.no_open_request': 'No hay una solicitud esperando respuesta.',
  'auth.confirmation_rate_limited': 'Espera un momento antes de pedir otro correo.',
  'security_token.not_found': 'El enlace no es válido o ya venció.',
  'security_token.already_used': 'Ese enlace ya se usó.',
  'security_token.expired': 'Ese enlace ya venció.',
  'guardian_consent.not_found': 'El enlace de autorización no es válido.',
  'guardian_consent.not_open': 'Esta autorización ya no está disponible.',
  'guardian_consent.email_not_verified': 'Primero verifica el correo del acudiente.',
  'guardian_consent.expired': 'El plazo para autorizar ya venció.',
  'password.current_invalid': 'La contraseña actual no coincide.',
  'password.invalid': 'La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.',
  'user.username_taken': 'Ese nombre de usuario ya está en uso.',
  'profile.username_change_limited': 'Aún no puedes cambiar tu nombre de usuario.',
  'profile.content_not_allowed': 'Ese texto no se puede usar en el perfil.',
  'bio.too_long': 'La bio puede tener hasta 160 caracteres.',
  'displayName.too_long': 'El nombre visible puede tener hasta 60 caracteres.',
  'displayName.required': 'Escribe el nombre visible.',
  'avatar.minor_photo_not_allowed': 'Por tu edad, el avatar es un ícono de la galería.',
  'avatar.too_large': 'La foto supera el tamaño permitido.',
  'avatar.invalid_image': 'Sube una imagen JPG, PNG o WebP.',
  'avatar.empty': 'Adjunta una foto.',
  'avatar.not_found': 'Ese ícono ya no está disponible.',
  'auth.current_device': 'Este es el dispositivo que estás usando. Ciérralo desde Cerrar sesión.',
  'geography.city_unavailable': 'Esa ciudad no está disponible.',
  'report.email_unconfirmed': 'Confirma tu correo para publicar.',
  'report.pending_guardian': 'Tu cuenta espera la autorización de un acudiente.',
  'report.cannot_publish': 'Tu cuenta no puede publicar.',
  'report.minor_public_identity': 'Si eres menor de edad, un reporte público tiene que ser anónimo. También puedes publicarlo solo para amigos.',
  'report.category_unavailable': 'Esa categoría no está disponible.',
  'report.adult_only': 'Esta categoría es solo para mayores de edad.',
  'report.media_required': 'Esta categoría pide al menos una foto o un video.',
  'report.media_not_found': 'No encontramos el archivo. Vuelve a subirlo.',
  'report.media_type': 'Sube una imagen JPG, PNG o WebP, o un video MP4.',
  'report.image_too_large': 'La imagen supera el tamaño permitido.',
  'report.video_too_large': 'El video supera el tamaño permitido.',
  'report.video_too_long': 'El video supera la duración permitida.',
  'report.video_unreadable': 'No pudimos leer la duración del video.',
  'report.too_many_files': 'El reporte supera el máximo de fotos o videos.',
  'report.invalid_visibility': 'Elige si el reporte es público o solo para amigos.',
  'report.invalid_confirmation': 'El voto debe ser sigue ahí o ya no está.',
  'report.not_found': 'No encontramos el reporte.',
  'report.not_available': 'Este reporte ya no está disponible.',
  'report.not_author': 'Solo quien creó el reporte puede cambiarlo.',
  'report.confirmation_own': 'No puedes confirmar tu propio reporte.',
  'report.confirmation_too_far': 'Tienes que estar cerca del lugar para confirmar el reporte.',
  'report.daily_limit': 'Llegaste al límite de reportes de tu plan por hoy.',
  'report.anonymous_daily_limit': 'Llegaste al límite de publicaciones anónimas de tu plan por hoy.',
  'comment.email_unconfirmed': 'Confirma tu correo para comentar.',
  'comment.pending_guardian': 'Tu cuenta espera la autorización de un acudiente.',
  'comment.cannot_publish': 'Tu cuenta no puede comentar ahora.',
  'comment.minor_public_identity': 'Si eres menor de edad, en un reporte público el comentario tiene que ser anónimo. En un reporte de solo amigos puedes usar tu nombre.',
  'comment.report_closed': 'Este reporte ya no admite comentarios.',
  'comment.not_found': 'No encontramos el comentario.',
  'comment.not_author': 'Solo quien escribió el comentario puede cambiarlo.',
  'comment.parent_invalid': 'Esa respuesta no se puede publicar ahí.',
  'comment.anonymous_daily_limit': 'Llegaste al límite de comentarios anónimos de hoy.',
  'reaction.not_found': 'No encontramos esa reacción.',
  'complaint.duplicate': 'Ya enviaste una denuncia sobre esto. La estamos revisando.',
  'complaint.self': 'No puedes denunciarte a ti mismo ni tu propio contenido.',
  'complaint.target_not_found': 'No encontramos lo que quieres denunciar.',
  'block.self': 'No puedes bloquearte a ti mismo.',
  'block.not_found': 'No encontramos ese bloqueo.',
  'account.password_invalid': 'La contraseña no coincide.',
  'notification.device_required': 'Vuelve a iniciar sesión para registrar este dispositivo.',
  'notification.device_not_found': 'Este dispositivo ya no está en tu cuenta.',
  'notification.proximity_disabled': 'Las alertas por cercanía están apagadas.',
  'notification.proximity_not_allowed_minor': 'Para las alertas por cercanía hace falta la autorización de tu representante.',
  'notification.alert_location_limited': 'Espera un momento antes de actualizar la ubicación.',
  'notification.timezone_invalid': 'Esa zona horaria no es válida.',
  'notification.quiet_hours': 'Elige la hora de inicio y la de fin, o desactiva el silencio.',
  'notification.city_not_found': 'No encontramos esa ciudad.',
  'notification.interest_city_home': 'Esa ya es tu ciudad principal.',
  'notification.interest_city_exists': 'Esa ciudad ya está en tu lista.',
  'notification.interest_city_limit': 'Llegaste al máximo de ciudades.',
  'notification.category_not_found': 'No encontramos esa categoría.',
  'notification.not_found': 'No encontramos esa alerta.',
  'friendship.self': 'No puedes enviarte una solicitud a ti mismo.',
  'friendship.already_friends': 'Ya son amigos.',
  'friendship.rate_limited': 'Llegaste al límite de solicitudes. Inténtalo más tarde.',
  'friendship.search_limited': 'Buscaste demasiadas veces. Espera un momento.',
  'friendship.not_pending': 'Esa solicitud ya no está pendiente.',
  'friendship.not_allowed': 'No puedes hacer eso con esta solicitud.',
  'friendship.not_found': 'No encontramos esa amistad.',
  'invitation.not_valid': 'Esta invitación ya no sirve.',
  'invitation.email_invalid': 'Ese correo no es válido.',
  'profile.not_available': 'No encontramos ese perfil.',
  'profile.minor_must_be_private': 'Un menor de edad tiene que mantener el perfil privado.',
  'profile.consent_required': 'Para hacer público el perfil tienes que aceptar el texto vigente.',
  'location.sharing_off': 'La ubicación compartida está apagada.',
  'location.rate_limited': 'Enviaste la ubicación demasiadas veces. Espera un momento.',
  'location.reading_rejected': 'Esa lectura de ubicación no se puede usar.',
  'location.consent_required': 'Para compartir ubicación tienes que aceptar el texto vigente.',
  'location.guardian_required': 'Para compartir ubicación, tu representante tiene que autorizarlo.',
  'location.minor_public': 'Un menor de edad no aparece en el mapa general.',
  'location.profile_private': 'El mapa general solo muestra perfiles públicos.',
  'location.zone_limit': 'Puedes guardar hasta 5 zonas de privacidad.',
  'location.zone_not_found': 'No encontramos esa zona.',
  'location.area_too_large': 'Acerca el mapa para ver personas.',
  'account_deletion.already_scheduled': 'Ya hay una solicitud de eliminación en curso.',
  'account_deletion.not_scheduled': 'No hay una eliminación que cancelar.',
  'live.minors_cannot_stream': 'En esta versión un menor no puede transmitir.',
  'live.minor_friends_only': 'Un menor solo puede transmitir a sus amigos.',
  'live.already_live': 'Ya tienes una transmisión activa.',
  'live.limit_concurrent': 'Ahora mismo no hay cupo para otra transmisión.',
  'live.viewers_full': 'Esta transmisión ya no admite más personas.',
  'live.not_found': 'No encontramos esa transmisión.',
  'live.forbidden': 'No puedes ver transmisiones.',
  'live.unauthorized': 'No autorizado.',
  'live.not_configured': 'La transmisión en vivo no está configurada.',
};

export function messageFor(code: AppErrorCode, apiCode?: string, detail?: string): string {
  const spoken = spokenDetail(detail);
  if (spoken) {
    return spoken;
  }

  if (apiCode && apiMessages[apiCode]) {
    return apiMessages[apiCode];
  }

  if (code === 'NOT_FOUND' && !apiCode) {
    return 'Esta solicitud todavía no está disponible.';
  }

  return errorMessages[code];
}

function spokenDetail(detail: string | undefined): string | undefined {
  if (!detail) {
    return undefined;
  }

  const text = detail.trim().replace(/\s+/g, ' ');
  if (text.length < 12 || text.length > 280) {
    return undefined;
  }

  if (/[\r\n<>{}]/.test(detail) || /exception|stack|trace|sqlstate|object reference|system\.|microsoft\.|npgsql/i.test(text)) {
    return undefined;
  }

  return text;
}
