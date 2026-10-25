import { blockEndpoint, confirm, feedbackMessages, notify } from '@situ/shared';
import type { QueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';

export async function blockContent(
  queryClient: QueryClient,
  input: { anonymous: boolean; reportId: string; commentId?: string; userId?: string | null },
): Promise<boolean> {
  const accepted = await confirm({
    title: feedbackMessages.blockUserTitle,
    message: input.anonymous ? feedbackMessages.blockAnonymousBody : feedbackMessages.blockUserBody,
    type: 'danger',
    confirmText: 'Bloquear',
    cancelText: 'Cancelar',
  });
  if (!accepted) {
    return false;
  }

  try {
    if (blockEndpoint(input.anonymous) === 'block-author') {
      if (input.commentId) {
        await api.blockCommentAuthor(input.commentId);
      } else {
        await api.blockReportAuthor(input.reportId);
      }
    } else if (input.userId) {
      await api.blockUser(input.userId);
    } else {
      return false;
    }

    await queryClient.invalidateQueries({ queryKey: ['reports-map'] });
    await queryClient.invalidateQueries({ queryKey: ['report', input.reportId] });
    await queryClient.invalidateQueries({ queryKey: ['comments', input.reportId] });
    notify.success(feedbackMessages.blockDone);
    return true;
  } catch (error) {
    notify.error(error);
    return false;
  }
}
