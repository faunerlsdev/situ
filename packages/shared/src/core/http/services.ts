import type {
  AddCommentBody,
  AddCommentResult,
  BlockListItem,
  CommentPage,
  ComplaintBody,
  ReactionResult,
} from '../../reports/interaction';
import type {
  AppConfig,
  ConfirmReportBody,
  ConfirmReportResponse,
  CreateReportBody,
  CreateReportResponse,
  MapItem,
  MapQuery,
  MapResponse,
  MyReportsResponse,
  ReportCatalog,
  ReportDetail,
  SessionProfile,
  UploadMediaResponse,
} from '../../reports/types';
import type {
  AuthorizeGuardianConsentRequest,
  AuthorizeGuardianConsentResponse,
  AuthSession,
  ChangePasswordRequest,
  CommentRequest,
  ConfirmEmailRequest,
  ContactRequest,
  DataRequest,
  DeclineGuardianConsentRequest,
  ListCitiesResponse,
  ListCountriesResponse,
  ListLegalDocumentsResponse,
  ListRegionsResponse,
  LoginRequest,
  PasswordResetRequest,
  GuardianConsentReview,
  GuardianConsentStatus,
  RegisterRequest,
  RegisterResponse,
  ResendConfirmationEmailRequest,
  ResetPasswordRequest,
  SubmitGuardianDataRequest,
} from '../../api/types';
import { AppError } from '../http/errors';
import { messageFor } from '../http/errorMessages';
import { createHttpClient } from './client';
import { toAppError } from './normalize';
import type { HttpClient, HttpClientOptions } from './client';
import { httpTimeouts } from '../config/env';

export type ApiClientOptions = {
  baseUrl: string;
  getAccessToken?: () => string | null;
  prepareRequest?: () => Promise<void>;
  refreshAccessToken?: () => Promise<string | null>;
  onSessionExpired?: () => void;
};

export type PublicMapPin = {
  categoryCode: string;
  categoryName: string;
  approximateTime: string;
  zone: string;
  latitude: number;
  longitude: number;
};

export function createServices(http: HttpClient) {
  return {
    listCountries() {
      return http.request<ListCountriesResponse>({ url: '/api/v1/geography/countries' });
    },
    listRegions(countryId: string) {
      return http.request<ListRegionsResponse>({
        url: `/api/v1/geography/countries/${encodeURIComponent(countryId)}/regions`,
      });
    },
    listCities(regionId: string) {
      return http.request<ListCitiesResponse>({
        url: `/api/v1/geography/regions/${encodeURIComponent(regionId)}/cities`,
      });
    },
    listLegalDocuments(countryId: string) {
      return http.request<ListLegalDocumentsResponse>({
        url: `/api/v1/legal/documents?countryId=${encodeURIComponent(countryId)}`,
      });
    },
    login(body: LoginRequest) {
      return http
        .request<unknown>({ method: 'POST', url: '/api/v1/auth/login', data: body, skipAuthRefresh: true })
        .then(readAuthSession);
    },
    listMyDevices() {
      return http.request<{ devices: SessionDevice[] }>({ url: '/api/v1/me/devices', requiresAuth: true });
    },
    revokeDevice(deviceId: string) {
      return http.request<void>({
        method: 'DELETE',
        url: `/api/v1/me/devices/${encodeURIComponent(deviceId)}`,
        requiresAuth: true,
      });
    },
    updateMyProfile(body: { userName?: string | null; displayName?: string | null; bio?: string | null; homeCityId?: string | null; preferAnonymousReports?: boolean | null }) {
      return http.request<void>({ method: 'PATCH', url: '/api/v1/me/profile', data: body, requiresAuth: true });
    },
    changePhone(body: { currentPassword: string; phone: string }) {
      return http.request<void>({ method: 'POST', url: '/api/v1/me/phone', data: body, requiresAuth: true });
    },
    acknowledgeAnonymityNotice() {
      return http.request<{ version: string; acceptedAt: string }>({ method: 'POST', url: '/api/v1/me/anonymity-notice', requiresAuth: true });
    },
    requestDataExport() {
      return http.request<{ exportId: string; expiresAt: string | null }>({ method: 'POST', url: '/api/v1/me/data-export', requiresAuth: true });
    },
    usernameAvailable(userName: string) {
      return http.request<{ available: boolean }>({
        url: `/api/v1/users/username-available?userName=${encodeURIComponent(userName)}`,
        requiresAuth: true,
      });
    },
    avatarGallery() {
      return http.request<{ items: { id: string; name: string; imageUrl: string; sortOrder: number }[] }>({
        url: '/api/v1/avatars/gallery',
        requiresAuth: true,
      });
    },
    setGalleryAvatar(galleryItemId: string) {
      return http.request<void>({
        method: 'PUT',
        url: '/api/v1/me/avatar',
        data: { galleryItemId },
        requiresAuth: true,
      });
    },
    removeAvatar() {
      return http.request<void>({ method: 'DELETE', url: '/api/v1/me/avatar', requiresAuth: true });
    },
    uploadAvatarPhoto(data: FormData, onProgress: (percent: number) => void, signal?: AbortSignal) {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/me/avatar/photo',
        data,
        timeoutMs: httpTimeouts.uploadMs,
        onUploadProgress: onProgress,
        signal,
        requiresAuth: true,
      });
    },
    register(body: RegisterRequest) {
      return http.request<RegisterResponse>({ method: 'POST', url: '/api/v1/auth/register', data: body });
    },
    guardianConsentStatus() {
      return http.request<GuardianConsentStatus>({ url: '/api/v1/guardianship/mine', requiresAuth: true });
    },
    requestGuardianConsent(body: { guardianEmail: string; minorNoticeAccepted: boolean; minorStatement?: string }) {
      return http.request<GuardianConsentStatus>({
        method: 'POST',
        url: '/api/v1/guardianship/mine',
        data: body,
        requiresAuth: true,
      });
    },
    resendGuardianConsent() {
      return http.request<GuardianConsentStatus>({
        method: 'POST',
        url: '/api/v1/guardianship/mine/resend',
        requiresAuth: true,
      });
    },
    changeGuardianEmail(guardianEmail: string) {
      return http.request<GuardianConsentStatus>({
        method: 'POST',
        url: '/api/v1/guardianship/mine/email',
        data: { guardianEmail },
        requiresAuth: true,
      });
    },
    acknowledgeMinorNotice() {
      return http.request<GuardianConsentStatus>({
        method: 'POST',
        url: '/api/v1/guardianship/mine/notice',
        requiresAuth: true,
      });
    },
    cancelGuardianRegistration() {
      return http.request<void>({ method: 'DELETE', url: '/api/v1/guardianship/mine', requiresAuth: true });
    },
    guardianConsentReview(token: string) {
      return http.request<GuardianConsentReview>({
        url: `/api/v1/guardianship/consent/review?token=${encodeURIComponent(token)}`,
      });
    },
    verifyGuardianEmailCode(token: string, code: string) {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/guardianship/consent/verify-code',
        data: { token, code },
      });
    },
    resendGuardianEmailCode(token: string) {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/guardianship/consent/resend-code',
        data: { token },
      });
    },
    optOutGuardianEmail(token: string) {
      return http.request<void>({ method: 'POST', url: '/api/v1/guardianship/consent/opt-out', data: { token } });
    },
    revokeGuardianConsent(token: string) {
      return http.request<void>({ method: 'POST', url: '/api/v1/guardianship/consent/revoke', data: { token } });
    },
    updateGuardianPermissions(body: { token: string; liveStreamingAuthorized: boolean; locationSharingAuthorized: boolean }) {
      return http.request<void>({ method: 'POST', url: '/api/v1/guardianship/consent/permissions', data: body });
    },
    confirmEmail(body: ConfirmEmailRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/auth/confirm-email', data: body });
    },
    resendConfirmationEmail(body: ResendConfirmationEmailRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/auth/confirm-email/resend', data: body });
    },
    requestPasswordReset(body: PasswordResetRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/auth/password-reset', data: body });
    },
    validateResetToken(token: string) {
      return http.request<void>({
        url: `/api/v1/auth/password-reset/validate?token=${encodeURIComponent(token)}`,
      });
    },
    resetPassword(body: ResetPasswordRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/auth/password-reset/confirm', data: body });
    },
    refresh(refreshToken: string) {
      return http
        .request<unknown>({
          method: 'POST',
          url: '/api/v1/auth/refresh',
          data: { refreshToken },
          skipAuthRefresh: true,
        })
        .then(readAuthSession);
    },
    signOut(refreshToken: string) {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/auth/sign-out',
        data: { refreshToken },
        skipAuthRefresh: true,
      });
    },
    notificationSettings() {
      return http.request<NotificationSettings>({ url: '/api/v1/me/notification-settings', requiresAuth: true });
    },
    updateNotificationSettings(body: NotificationSettings) {
      return http.request<NotificationSettings>({
        method: 'PUT',
        url: '/api/v1/me/notification-settings',
        data: body,
        requiresAuth: true,
      });
    },
    interestCities() {
      return http.request<InterestCities>({ url: '/api/v1/me/interest-cities', requiresAuth: true });
    },
    addInterestCity(cityId: string) {
      return http.request<InterestCities>({
        method: 'PUT',
        url: `/api/v1/me/interest-cities/${encodeURIComponent(cityId)}`,
        requiresAuth: true,
      });
    },
    removeInterestCity(cityId: string) {
      return http.request<InterestCities>({
        method: 'DELETE',
        url: `/api/v1/me/interest-cities/${encodeURIComponent(cityId)}`,
        requiresAuth: true,
      });
    },
    categoryInterests() {
      return http.request<{ mutedCategoryIds: string[] }>({ url: '/api/v1/me/category-interests', requiresAuth: true });
    },
    updateCategoryInterests(mutedCategoryIds: string[]) {
      return http.request<{ mutedCategoryIds: string[] }>({
        method: 'PUT',
        url: '/api/v1/me/category-interests',
        data: { mutedCategoryIds },
        requiresAuth: true,
      });
    },
    setPushToken(token: string) {
      return http.request<void>({
        method: 'PUT',
        url: '/api/v1/devices/current/push-token',
        data: { token },
        requiresAuth: true,
      });
    },
    clearPushToken() {
      return http.request<void>({
        method: 'DELETE',
        url: '/api/v1/devices/current/push-token',
        requiresAuth: true,
      });
    },
    setAlertLocation(body: { latitude: number; longitude: number; accuracyMeters: number | null }) {
      return http.request<void>({
        method: 'PUT',
        url: '/api/v1/devices/current/alert-location',
        data: body,
        requiresAuth: true,
      });
    },
    clearAlertLocation() {
      return http.request<void>({
        method: 'DELETE',
        url: '/api/v1/devices/current/alert-location',
        requiresAuth: true,
      });
    },
    listNotifications(cursor?: string | null) {
      const query = new URLSearchParams({ limit: '20' });
      if (cursor) {
        query.set('cursor', cursor);
      }
      return http.request<NotificationPage>({ url: `/api/v1/notifications?${query.toString()}`, requiresAuth: true });
    },
    unreadNotificationCount() {
      return http.request<{ count: number }>({ url: '/api/v1/notifications/unread-count', requiresAuth: true });
    },
    markNotificationRead(id: string) {
      return http.request<void>({
        method: 'POST',
        url: `/api/v1/notifications/${encodeURIComponent(id)}/read`,
        requiresAuth: true,
      });
    },
    markAllNotificationsRead() {
      return http.request<void>({ method: 'POST', url: '/api/v1/notifications/read-all', requiresAuth: true });
    },
    changePassword(body: ChangePasswordRequest) {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/me/password',
        data: body,
        requiresAuth: true,
      });
    },
    submitGuardianData(body: SubmitGuardianDataRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/guardianship/consent/data', data: body });
    },
    verifyGuardianEmail(token: string) {
      return http.request<void>({
        url: `/api/v1/guardianship/consent/verify-email?token=${encodeURIComponent(token)}`,
      });
    },
    authorizeGuardianConsent(body: AuthorizeGuardianConsentRequest) {
      return http.request<AuthorizeGuardianConsentResponse>({
        method: 'POST',
        url: '/api/v1/guardianship/consent/authorize',
        data: body,
      });
    },
    declineGuardianConsent(body: DeclineGuardianConsentRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/guardianship/consent/decline', data: body });
    },
    sessionProfile() {
      return http.request<SessionProfile>({ url: '/api/v1/me' });
    },
    appConfig(ifNoneMatch?: string) {
      return http.requestResponse<AppConfig>({
        url: '/api/v1/app-config',
        headers: ifNoneMatch ? { 'If-None-Match': ifNoneMatch } : undefined,
        validateStatus: (status) => (status >= 200 && status < 300) || status === 304,
      });
    },
    resendMyConfirmationEmail() {
      return http.request<void>({ method: 'POST', url: '/api/v1/auth/confirm-email/resend-self' });
    },
    reportCategories(ifNoneMatch?: string) {
      return http.requestResponse<ReportCatalog>({
        url: '/api/v1/report-categories',
        headers: {
          'Accept-Language': 'es-CO',
          ...(ifNoneMatch ? { 'If-None-Match': ifNoneMatch } : {}),
        },
        validateStatus: (status) => (status >= 200 && status < 300) || status === 304,
      });
    },
    mapReports(query: MapQuery, signal?: AbortSignal) {
      const params = new URLSearchParams();
      params.set('minLat', String(query.minLat));
      params.set('minLon', String(query.minLon));
      params.set('maxLat', String(query.maxLat));
      params.set('maxLon', String(query.maxLon));
      params.set('zoom', String(query.zoom));
      if (query.categories.length > 0) {
        params.set('categories', query.categories.join(','));
      }
      if (query.since) {
        params.set('since', query.since);
      }
      if (query.friendsOnly) {
        params.set('type', 'friends');
      }
      if (query.latitude !== null && query.longitude !== null) {
        params.set('lat', String(query.latitude));
        params.set('lon', String(query.longitude));
      }
      return http.request<MapResponse>({
        url: `/api/v1/reports/map?${params.toString()}`,
        requiresAuth: true,
        signal,
      });
    },
    publicMap(query: { minLat: number; minLon: number; maxLat: number; maxLon: number }, signal?: AbortSignal) {
      const params = new URLSearchParams();
      params.set('minLat', String(query.minLat));
      params.set('minLon', String(query.minLon));
      params.set('maxLat', String(query.maxLat));
      params.set('maxLon', String(query.maxLon));
      return http.request<{ items: PublicMapPin[] }>({
        url: `/api/v1/public/reports/map?${params.toString()}`,
        signal,
      });
    },
    submitTakedown(body: { requesterName: string; requesterEmail: string; targetType: string; targetId: string; description: string; captchaToken: string }) {
      return http.request<{ accepted: boolean; trackingCode: string }>({
        method: 'POST',
        url: '/api/v1/takedown-requests',
        data: body,
      });
    },
    batchReports(ids: string[], signal?: AbortSignal) {
      const params = new URLSearchParams();
      params.set('ids', ids.join(','));
      return http.request<{ items: MapItem[] }>({
        url: `/api/v1/reports/batch?${params.toString()}`,
        requiresAuth: true,
        signal,
      });
    },
    report(id: string, signal?: AbortSignal) {
      return http.request<ReportDetail>({
        url: `/api/v1/reports/${encodeURIComponent(id)}`,
        requiresAuth: true,
        signal,
      });
    },
    myReports(cursor?: string, limit = 20) {
      const params = new URLSearchParams({ limit: String(limit) });
      if (cursor) {
        params.set('cursor', cursor);
      }
      return http.request<MyReportsResponse>({
        url: `/api/v1/reports/mine?${params.toString()}`,
        requiresAuth: true,
      });
    },
    createReport(body: CreateReportBody) {
      return http.request<CreateReportResponse>({
        method: 'POST',
        url: '/api/v1/reports',
        data: body,
        requiresAuth: true,
      });
    },
    updateReport(id: string, description: string) {
      return http.request<void>({
        method: 'PATCH',
        url: `/api/v1/reports/${encodeURIComponent(id)}`,
        data: { description },
        requiresAuth: true,
      });
    },
    deleteReport(id: string) {
      return http.request<void>({
        method: 'DELETE',
        url: `/api/v1/reports/${encodeURIComponent(id)}`,
        requiresAuth: true,
      });
    },
    confirmReport(id: string, body: ConfirmReportBody) {
      return http.request<ConfirmReportResponse>({
        method: 'PUT',
        url: `/api/v1/reports/${encodeURIComponent(id)}/confirmation`,
        data: body,
        requiresAuth: true,
      });
    },
    uploadReportMedia(data: FormData, onProgress: (percent: number) => void, signal?: AbortSignal) {
      return http.request<UploadMediaResponse>({
        method: 'POST',
        url: '/api/v1/reports/media',
        data,
        timeoutMs: httpTimeouts.uploadMs,
        onUploadProgress: onProgress,
        signal,
        requiresAuth: true,
      });
    },
    comments(reportId: string, cursor?: string) {
      const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
      return http.request<CommentPage>({
        method: 'GET',
        url: `/api/v1/reports/${encodeURIComponent(reportId)}/comments${query}`,
        requiresAuth: true,
      });
    },
    addComment(reportId: string, body: AddCommentBody) {
      return http.request<AddCommentResult>({
        method: 'POST',
        url: `/api/v1/reports/${encodeURIComponent(reportId)}/comments`,
        data: body,
        requiresAuth: true,
      });
    },
    editComment(id: string, text: string) {
      return http.request<{ id: string; text: string }>({
        method: 'PATCH',
        url: `/api/v1/comments/${encodeURIComponent(id)}`,
        data: { text },
        requiresAuth: true,
      });
    },
    deleteComment(id: string) {
      return http.request<void>({
        method: 'DELETE',
        url: `/api/v1/comments/${encodeURIComponent(id)}`,
        requiresAuth: true,
      });
    },
    setReportReaction(id: string, type: string) {
      return http.request<ReactionResult>({
        method: 'PUT',
        url: `/api/v1/reports/${encodeURIComponent(id)}/reaction`,
        data: { type },
        requiresAuth: true,
      });
    },
    clearReportReaction(id: string) {
      return http.request<ReactionResult>({
        method: 'DELETE',
        url: `/api/v1/reports/${encodeURIComponent(id)}/reaction`,
        requiresAuth: true,
      });
    },
    setCommentReaction(id: string, type: string) {
      return http.request<ReactionResult>({
        method: 'PUT',
        url: `/api/v1/comments/${encodeURIComponent(id)}/reaction`,
        data: { type },
        requiresAuth: true,
      });
    },
    clearCommentReaction(id: string) {
      return http.request<ReactionResult>({
        method: 'DELETE',
        url: `/api/v1/comments/${encodeURIComponent(id)}/reaction`,
        requiresAuth: true,
      });
    },
    submitComplaint(body: ComplaintBody) {
      return http.request<{ id: string; status: string; isPriority: boolean }>({
        method: 'POST',
        url: '/api/v1/complaints',
        data: body,
        requiresAuth: true,
      });
    },
    blocks() {
      return http.request<{ blocks: BlockListItem[] }>({
        method: 'GET',
        url: '/api/v1/blocks',
        requiresAuth: true,
      });
    },
    blockUser(userId: string) {
      return http.request<{ blocked: boolean }>({
        method: 'POST',
        url: '/api/v1/blocks',
        data: { userId },
        requiresAuth: true,
      });
    },
    unblock(blockId: string) {
      return http.request<void>({
        method: 'DELETE',
        url: `/api/v1/blocks/${encodeURIComponent(blockId)}`,
        requiresAuth: true,
      });
    },
    blockReportAuthor(id: string) {
      return http.request<{ blocked: boolean }>({
        method: 'POST',
        url: `/api/v1/reports/${encodeURIComponent(id)}/block-author`,
        requiresAuth: true,
      });
    },
    blockCommentAuthor(id: string) {
      return http.request<{ blocked: boolean }>({
        method: 'POST',
        url: `/api/v1/comments/${encodeURIComponent(id)}/block-author`,
        requiresAuth: true,
      });
    },
    requestAccountDeletion(password: string) {
      return http.request<{ scheduledFor: string }>({
        method: 'POST',
        url: '/api/v1/me/deletion-request',
        data: { password },
        requiresAuth: true,
      });
    },
    cancelAccountDeletion() {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/me/deletion-request/cancel',
        requiresAuth: true,
      });
    },
    commentOnReport(reportId: string, body: CommentRequest) {
      return http.request<void>({
        method: 'POST',
        url: `/api/v1/reports/${encodeURIComponent(reportId)}/comments`,
        data: body,
        requiresAuth: true,
      });
    },
    submitContact(body: ContactRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/support/contact', data: body });
    },
    submitDataRequest(body: DataRequest) {
      return http.request<void>({ method: 'POST', url: '/api/v1/support/data-requests', data: body });
    },
    upload<T>(url: string, data: FormData, onProgress: (percent: number) => void, signal?: AbortSignal) {
      return http.request<T>({
        method: 'POST',
        url,
        data,
        timeoutMs: httpTimeouts.uploadMs,
        onUploadProgress: onProgress,
        signal,
        requiresAuth: true,
      });
    },
  };
}

export function createApiClient(options: ApiClientOptions) {
  const httpOptions: HttpClientOptions = {
    baseUrl: options.baseUrl,
    getAccessToken: options.getAccessToken,
    prepareRequest: options.prepareRequest,
    refreshAccessToken: options.refreshAccessToken,
    onSessionExpired: options.onSessionExpired,
  };
  return createServices(createHttpClient(httpOptions));
}

export type NotificationSettings = {
  pushEnabled: boolean;
  nearbyRadiusMeters: number;
  notifyNearbyReports: boolean;
  notifyFriendReports: boolean;
  notifyFriendLives: boolean;
  notifyImportantNews: boolean;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
  timeZoneId: string;
};

export type InterestCities = {
  cities: { cityId: string; name: string }[];
  maximum: number;
};

export type InboxNotification = {
  id: string;
  type: string;
  title: string;
  body: string;
  targetType: string | null;
  targetId: string | null;
  reportId: string | null;
  createdAt: string;
  isRead: boolean;
  available: boolean;
};

export type NotificationPage = {
  notifications: InboxNotification[];
  nextCursor: string | null;
};

export type SessionDevice = {
  id: string;
  deviceIdentifier: string;
  platform: 'IOS' | 'Android' | 'Web';
  deviceName: string | null;
  lastSeenAt: string;
  isActive: boolean;
  isCurrent?: boolean;
};

function readAuthSession(value: unknown): AuthSession {
  if (typeof value !== 'object' || value === null) {
    throw new AppError({ code: 'UNKNOWN', userMessage: messageFor('UNKNOWN') });
  }

  const record = value as Record<string, unknown>;
  const accessToken = record.accessToken;
  const refreshToken = record.refreshToken;
  if (typeof accessToken !== 'string' || accessToken.length === 0 || typeof refreshToken !== 'string' || refreshToken.length === 0) {
    if (typeof record.detail === 'string' || typeof record.code === 'string') {
      throw toAppError({ status: typeof record.status === 'number' ? record.status : 400, data: value });
    }

    throw new AppError({ code: 'UNKNOWN', userMessage: messageFor('UNKNOWN') });
  }

  const accessTokenExpiresAt = typeof record.accessTokenExpiresAt === 'string' ? record.accessTokenExpiresAt : undefined;
  return { accessToken, refreshToken, accessTokenExpiresAt };
}
