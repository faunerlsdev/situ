export type DistanceUnit = 'Kilometers' | 'Miles';

export type Country = {
  id: string;
  isoCode: string;
  name: string;
  regionLabel: string;
  defaultLocale: string;
  defaultCurrency: string;
  defaultTimeZone: string;
  distanceUnit: DistanceUnit;
  minimumAge: number;
  isEnabled: boolean;
};

export type ListCountriesResponse = {
  countries: Country[];
};

export type DevicePlatform = 'IOS' | 'Android' | 'Web';

export type LoginRequest = {
  login: string;
  password: string;
  deviceIdentifier: string;
  platform: DevicePlatform;
  deviceName?: string | null;
  appVersion?: string | null;
  pushToken?: string | null;
};

export type AuthSession = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt?: string;
};

export type Region = {
  id: string;
  countryId: string;
  name: string;
  code: string | null;
};

export type ListRegionsResponse = {
  regions: Region[];
};

export type City = {
  id: string;
  regionId: string;
  countryId: string;
  name: string;
};

export type ListCitiesResponse = {
  cities: City[];
};

export type LegalDocumentType =
  | 'TermsOfUse'
  | 'PrivacyPolicy'
  | 'PublicProfileConsent'
  | 'LocationSharingConsent'
  | 'PersonalizedAdsConsent'
  | 'GuardianNotice';

export type LegalDocument = {
  id: string;
  type: LegalDocumentType;
  version: string;
  url: string;
  locale: string;
};

export type ListLegalDocumentsResponse = {
  documents: LegalDocument[];
};

export type RegisterRequest = {
  email: string;
  userName: string;
  givenNames: string;
  surnames: string;
  phone: string;
  password: string;
  dateOfBirth: string;
  countryId: string;
  regionId: string;
  cityId: string;
  acceptedDocumentIds: string[];
  guardianEmail?: string;
  minorNoticeAccepted?: boolean;
};

export type RegisterResponse = {
  userId: string;
  status: string;
};

export type GuardianConsentStatus = {
  status: 'NotRequested' | 'Requested' | 'EmailVerified' | 'Authorized' | 'Declined' | 'Expired' | 'Revoked';
  maskedEmail: string | null;
  expiresAt: string | null;
  resendsRemaining: number;
  retryAfter: string | null;
  minorNoticeRecorded: boolean;
};

export type GuardianConsentReview = {
  userName: string;
  age: number;
  city: string;
  minorStatement: string | null;
  noticeVersion: string;
  expiresAt: string;
  requiresLiveAuthorization: boolean;
};

export type ConfirmEmailRequest = {
  token: string;
};

export type ResendConfirmationEmailRequest = {
  email: string;
};

export type PasswordResetRequest = {
  email: string;
};

export type ResetPasswordRequest = {
  token: string;
  password: string;
};

export type ChangePasswordRequest = {
  currentPassword: string;
  newPassword: string;
};

export type GuardianDocumentType = 'CitizenshipCard' | 'ForeignerCard' | 'Passport' | 'Other';

export type SubmitGuardianDataRequest = {
  token: string;
  fullName: string;
  documentType: GuardianDocumentType;
  documentNumber: string;
  contactEmail: string;
};

export type AuthorizeGuardianConsentRequest = {
  token: string;
  liveStreamingAuthorized: boolean;
  locationSharingAuthorized: boolean;
  dataProcessingAccepted?: boolean;
};

export type AuthorizeGuardianConsentResponse = {
  userId: string;
  status: string;
};

export type DeclineGuardianConsentRequest = {
  token: string;
  reason?: string;
};

export type CommentRequest = {
  body: string;
};

export type ContactRequest = {
  name: string;
  email: string;
  message: string;
};

export type DataRequestKind = 'access' | 'correction' | 'deletion' | 'contentRemoval';

export type DataRequest = {
  name: string;
  email: string;
  kind: DataRequestKind;
  message: string;
  contentUrl?: string;
};

export type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  retryAfter?: string;
};
