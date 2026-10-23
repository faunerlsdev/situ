export const httpTimeouts = {
  defaultMs: 15_000,
  uploadMs: 120_000,
} as const;

export function readApiUrl(value: string | undefined, fallback: string): string {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : fallback;
}
