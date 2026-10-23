export function webDeviceId(): string {
  const key = 'situ.deviceId';
  const existing = window.localStorage.getItem(key);
  if (existing) {
    return existing;
  }

  const created = window.crypto.randomUUID();
  window.localStorage.setItem(key, created);
  return created;
}
