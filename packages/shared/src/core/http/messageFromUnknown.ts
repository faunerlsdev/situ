import { isAppError } from './errors';
import { errorMessages } from './errorMessages';

export function messageFromUnknown(error: unknown): string {
  if (isAppError(error)) {
    return error.code === 'CANCELLED' ? '' : error.userMessage;
  }

  return errorMessages.NETWORK;
}
