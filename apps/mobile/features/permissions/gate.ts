export type OsPermission = 'granted' | 'denied' | 'undetermined';

export type PermissionStep = 'open' | 'explain' | 'settings';

export function readOsPermission(status: string | null | undefined): OsPermission {
  if (status === 'granted') {
    return 'granted';
  }
  if (status === 'denied') {
    return 'denied';
  }
  return 'undetermined';
}

/** One denied permission blocks the rest. All granted opens the feature. Anything else explains first. */
export function permissionStep(statuses: readonly OsPermission[]): PermissionStep {
  if (statuses.some((status) => status === 'denied')) {
    return 'settings';
  }
  if (statuses.length > 0 && statuses.every((status) => status === 'granted')) {
    return 'open';
  }
  return 'explain';
}

export async function ensurePermissions(input: {
  statuses: () => Promise<readonly OsPermission[]>;
  explain: () => Promise<boolean>;
  request: () => Promise<boolean>;
  settings: () => Promise<boolean>;
  openSettings: () => Promise<void>;
}): Promise<boolean> {
  const step = permissionStep(await input.statuses());
  if (step === 'open') {
    return true;
  }

  if (step === 'settings') {
    if (await input.settings()) {
      await input.openSettings();
    }
    return false;
  }

  if (!(await input.explain())) {
    return false;
  }

  if (await input.request()) {
    return true;
  }

  if (await input.settings()) {
    await input.openSettings();
  }
  return false;
}
