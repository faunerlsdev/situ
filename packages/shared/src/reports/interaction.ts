import { apiMessages } from '../core/http/errorMessages';
import { feedbackMessages } from '../core/feedback/messages';

export const commentLimit = 500;

export const reactionKinds = ['Like', 'Love', 'Haha', 'Wow', 'Sad', 'Angry'] as const;
export type ReactionKind = (typeof reactionKinds)[number];

export const reactionLabels: Record<ReactionKind, string> = {
  Like: 'Me gusta',
  Love: 'Me encanta',
  Haha: 'Me divierte',
  Wow: 'Me asombra',
  Sad: 'Me entristece',
  Angry: 'Me enoja',
};

export const complaintReasons = [
  { code: 'FalseInformation', label: 'Información falsa' },
  { code: 'Harassment', label: 'Acoso' },
  { code: 'Violence', label: 'Violencia' },
  { code: 'SexualContent', label: 'Contenido sexual' },
  { code: 'MinorSafety', label: 'Seguridad de menores' },
  { code: 'PrivacyViolation', label: 'Violación de privacidad' },
  { code: 'Spam', label: 'Spam' },
  { code: 'Other', label: 'Otro' },
] as const;

export type ComplaintReasonCode = (typeof complaintReasons)[number]['code'];

export type ReactionCount = {
  type: string;
  count: number;
};

export type ReactionSnapshot = {
  counts: ReactionCount[];
  mine: string | null;
};

export type BlockListItem = {
  blockId: string;
  scope: string;
  userId?: string | null;
  displayName: string;
  originLabel?: string | null;
};

export function composerAnonymity(input: { isMinor: boolean; visibility: string; chosen: boolean }): {
  anonymous: boolean;
  locked: boolean;
} {
  if (input.isMinor && input.visibility !== 'FriendsOnly') {
    return { anonymous: true, locked: true };
  }

  return { anonymous: input.chosen, locked: false };
}

export function commentValidation(text: string): string | null {
  const value = text.trim();
  if (value.length === 0) {
    return feedbackMessages.commentEmpty;
  }

  if (value.length > commentLimit) {
    return feedbackMessages.commentTooLong;
  }

  return null;
}

export function rateLimitCountdown(retryAfter: string | undefined, now = Date.now()): string {
  if (!retryAfter) {
    return apiMessages['auth.rate_limited'] ?? feedbackMessages.rateLimited;
  }

  const ends = Date.parse(retryAfter);
  if (Number.isNaN(ends)) {
    return apiMessages['auth.rate_limited'] ?? feedbackMessages.rateLimited;
  }

  const seconds = Math.max(0, Math.ceil((ends - now) / 1000));
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  if (minutes > 0) {
    return `Demasiados intentos. Espera ${minutes} min ${rest} s.`;
  }

  return `Demasiados intentos. Espera ${seconds} s.`;
}

export type WriteFailure =
  | { kind: 'inline'; message: string }
  | { kind: 'countdown'; message: string }
  | { kind: 'modal'; message: string }
  | { kind: 'duplicate'; message: string }
  | { kind: 'network' }
  | { kind: 'toast'; message: string };

export function classifyWriteError(error: { code?: string; apiCode?: string; retryAfter?: string }, now = Date.now()): WriteFailure {
  if (error.code === 'NETWORK' || error.code === 'TIMEOUT') {
    return { kind: 'network' };
  }

  if (error.apiCode === 'auth.rate_limited' || error.code === 'RATE_LIMIT') {
    return { kind: 'countdown', message: rateLimitCountdown(error.retryAfter, now) };
  }

  if (error.apiCode === 'comment.anonymous_daily_limit') {
    return { kind: 'inline', message: apiMessages['comment.anonymous_daily_limit'] ?? feedbackMessages.anonymousDailyLimit };
  }

  if (error.apiCode === 'comment.minor_public_identity') {
    return { kind: 'modal', message: apiMessages['comment.minor_public_identity'] ?? feedbackMessages.minorPublicComment };
  }

  if (error.apiCode === 'complaint.duplicate') {
    return { kind: 'duplicate', message: apiMessages['complaint.duplicate'] ?? feedbackMessages.complaintDuplicate };
  }

  if (error.code === 'VALIDATION') {
    return { kind: 'inline', message: feedbackMessages.commentInvalid };
  }

  const known = error.apiCode ? apiMessages[error.apiCode] : undefined;
  return { kind: 'toast', message: known ?? feedbackMessages.writeFailed };
}

export function toggleReaction(snapshot: ReactionSnapshot, type: string): ReactionSnapshot {
  const next = snapshot.mine === type ? null : type;
  return { mine: next, counts: recount(snapshot.counts, snapshot.mine, next) };
}

export function rollbackReaction(previous: ReactionSnapshot): ReactionSnapshot {
  return { mine: previous.mine, counts: previous.counts.map((item) => ({ ...item })) };
}

function recount(counts: ReactionCount[], previous: string | null, next: string | null): ReactionCount[] {
  const totals = new Map<string, number>();
  for (const item of counts) {
    totals.set(item.type, item.count);
  }

  if (previous) {
    totals.set(previous, Math.max(0, (totals.get(previous) ?? 0) - 1));
  }

  if (next) {
    totals.set(next, (totals.get(next) ?? 0) + 1);
  }

  return reactionKinds
    .map((type) => ({ type, count: totals.get(type) ?? 0 }))
    .filter((item) => item.count > 0);
}

export function blockEndpoint(authorIsAnonymous: boolean): 'block-author' | 'blocks' {
  return authorIsAnonymous ? 'block-author' : 'blocks';
}

export function blockRowLabel(block: Pick<BlockListItem, 'scope' | 'displayName' | 'originLabel'>): string {
  if (block.scope === 'AnonymousContentOnly') {
    return block.originLabel ? `Usuario anónimo · ${block.originLabel}` : 'Usuario anónimo';
  }

  return block.displayName;
}

/** True when an anonymous block row exposes a real account. */
export function anonymousBlockLeaks(block: Pick<BlockListItem, 'scope' | 'userId' | 'displayName'>): boolean {
  if (block.scope !== 'AnonymousContentOnly') {
    return false;
  }

  if (block.userId) {
    return true;
  }

  return block.displayName !== 'Usuario anónimo';
}

export type CommentAuthor = {
  userId: string | null;
  displayName: string;
  avatarUrl: string | null;
  memberSinceYear: number | null;
  memberSinceMonth: number | null;
  isAnonymous: boolean;
  hideAuthorOnMap: boolean;
};

export type CommentItem = {
  id: string;
  text: string;
  author: CommentAuthor;
  createdAt: string;
  reactionCounts: ReactionCount[];
  myReaction: string | null;
  replies: CommentItem[];
  isMine: boolean;
};

export type CommentPage = {
  comments: CommentItem[];
  nextCursor: string | null;
};

export type AddCommentBody = {
  text: string;
  isAnonymous: boolean;
  parentCommentId?: string | null;
};

export type AddCommentResult = {
  id: string;
  text: string;
  author: CommentAuthor;
  createdAt: string;
  commentCount: number;
};

export type ComplaintBody = {
  targetType: 'Report' | 'Comment' | 'User';
  targetId: string;
  reason: ComplaintReasonCode;
  details?: string | null;
};

export type ReactionResult = {
  type: string | null;
  counts: ReactionCount[];
};

export function deletionDateLabel(iso: string | null | undefined): string {
  if (!iso) {
    return feedbackMessages.deletionPendingUnknown;
  }

  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return feedbackMessages.deletionPendingUnknown;
  }

  return date.toLocaleDateString('es-CO', { day: 'numeric', month: 'long', year: 'numeric' });
}
