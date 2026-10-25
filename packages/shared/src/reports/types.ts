import type { components } from '../schema';

type Schemas = components['schemas'];

export type ReportCategory = Schemas['ReportCategoryNode'];
export type ReportCatalog = Schemas['GetReportCatalogResponse'];
export type ReportAuthor = Schemas['ReportAuthorView'];
export type ReportCard = Schemas['ReportCardView'];
export type ReportMedia = Schemas['ReportMediaView'];
export type ReportComment = Schemas['ReportCommentView'];
export type ReportDetail = Schemas['GetReportResponse'];
export type MapItem = Schemas['MapItemView'];
export type MapResponse = Schemas['GetReportMapResponse'];
export type MyReportsResponse = Schemas['ListMyReportsResponse'];
export type CreateReportBody = Schemas['CreateReportBody'];
export type CreateReportResponse = Schemas['CreateReportResponse'];
export type ConfirmReportBody = Schemas['ConfirmReportBody'];
export type ConfirmReportResponse = Schemas['ConfirmReportResponse'];
export type UploadMediaResponse = Schemas['UploadReportMediaResponse'];
export type MediaRequirement = Schemas['MediaRequirement'];
export type AppConfig = Schemas['GetAppConfigResponse'];
export type SessionProfile = Schemas['GetSessionProfileResponse'];
export type MediaLimitsView = Schemas['MediaLimitsView'];

export type ReportVisibility = 'Public' | 'FriendsOnly';
export type ConfirmationType = 'StillThere' | 'GoneNow';

/** Query string of GET /reports/map. The OpenAPI document lists these as separate parameters. */
export type MapQuery = {
  minLat: number;
  minLon: number;
  maxLat: number;
  maxLon: number;
  zoom: number;
  categories: string[];
  since: 'hour' | 'today' | null;
  friendsOnly: boolean;
  latitude: number | null;
  longitude: number | null;
};
