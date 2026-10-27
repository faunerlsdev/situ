let pendingReportId: string | null = null;

export function rememberReportLink(id: string): void {
  pendingReportId = id;
}

export function takeReportLink(): string | null {
  const id = pendingReportId;
  pendingReportId = null;
  return id;
}
