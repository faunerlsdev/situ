export type ShareCard = {
  indexable: boolean;
  categoryName?: string | null;
  zone?: string | null;
};

export function reportRobots(share: ShareCard | null): { index: boolean; follow: boolean } {
  if (!share?.indexable) {
    return { index: false, follow: false };
  }

  return { index: true, follow: true };
}

export function shareTitle(share: ShareCard | null) {
  if (!share?.indexable || !share.categoryName) {
    return 'SITU';
  }

  return share.categoryName;
}

export function shareDescription(share: ShareCard | null) {
  if (!share?.indexable) {
    return undefined;
  }

  return share.zone ?? undefined;
}
