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
import type { HttpClient, HttpClientOptions } from './client';
import { httpTimeouts } from '../config/env';

export type ApiClientOptions = {
  baseUrl: string;
  getAccessToken?: () => string | null;
  prepareRequest?: () => Promise<void>;
  refreshAccessToken?: () => Promise<string | null>;
  onSessionExpired?: () => void;
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
    changePassword(body: ChangePasswordRequest) {
      return http.request<void>({
        method: 'POST',
        url: '/api/v1/auth/password',
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

function readAuthSession(value: unknown): AuthSession {
  if (typeof value !== 'object' || value === null) {
    throw new AppError({ code: 'UNKNOWN', userMessage: messageFor('UNKNOWN') });
  }

  const record = value as Record<string, unknown>;
  const accessToken = record.accessToken;
  const refreshToken = record.refreshToken;
  if (typeof accessToken !== 'string' || accessToken.length === 0 || typeof refreshToken !== 'string' || refreshToken.length === 0) {
    throw new AppError({ code: 'UNKNOWN', userMessage: messageFor('UNKNOWN') });
  }

  const accessTokenExpiresAt = typeof record.accessTokenExpiresAt === 'string' ? record.accessTokenExpiresAt : undefined;
  return { accessToken, refreshToken, accessTokenExpiresAt };
}
