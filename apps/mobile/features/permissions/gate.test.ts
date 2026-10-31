import { describe, expect, it, vi } from 'vitest';

import { ensurePermissions, permissionStep } from './gate';

describe('permission gate', () => {
  it('opens directly when the permission is already granted', async () => {
    const explain = vi.fn(async () => true);
    const request = vi.fn(async () => true);
    const settings = vi.fn(async () => true);
    const openSettings = vi.fn(async () => undefined);

    const opened = await ensurePermissions({
      statuses: async () => ['granted'],
      explain,
      request,
      settings,
      openSettings,
    });

    expect(opened).toBe(true);
    expect(explain).not.toHaveBeenCalled();
    expect(request).not.toHaveBeenCalled();
    expect(settings).not.toHaveBeenCalled();
    expect(openSettings).not.toHaveBeenCalled();
  });

  it('explains only while the system has not decided, then requests', async () => {
    const explain = vi.fn(async () => true);
    const request = vi.fn(async () => true);

    const opened = await ensurePermissions({
      statuses: async () => ['undetermined'],
      explain,
      request,
      settings: async () => false,
      openSettings: async () => undefined,
    });

    expect(opened).toBe(true);
    expect(explain).toHaveBeenCalledOnce();
    expect(request).toHaveBeenCalledOnce();
  });

  it('skips the explanation when a required permission is denied', async () => {
    const explain = vi.fn(async () => true);
    const request = vi.fn(async () => true);
    const settings = vi.fn(async () => false);

    const opened = await ensurePermissions({
      statuses: async () => ['granted', 'denied'],
      explain,
      request,
      settings,
      openSettings: async () => undefined,
    });

    expect(opened).toBe(false);
    expect(permissionStep(['granted', 'denied'])).toBe('settings');
    expect(explain).not.toHaveBeenCalled();
    expect(request).not.toHaveBeenCalled();
    expect(settings).toHaveBeenCalledOnce();
  });
});
