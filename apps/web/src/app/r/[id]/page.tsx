import type { Metadata } from 'next';
import { ReportPageClient } from './view';
import { reportRobots, shareDescription, shareTitle } from '@/features/reports/share-meta';
import type { ShareCard } from '@/features/reports/share-meta';
import { http } from '@/lib/http';

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  const share = await loadShare(id);
  const robots = reportRobots(share);
  return {
    title: shareTitle(share),
    description: shareDescription(share),
    robots,
    openGraph: robots.index
      ? { title: shareTitle(share), description: shareDescription(share) }
      : undefined,
  };
}

export default async function ReportRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <ReportPageClient id={id} />;
}

async function loadShare(id: string): Promise<ShareCard | null> {
  try {
    const response = await http.requestResponse<ShareCard>({
      url: `/api/v1/reports/${id}/share`,
      skipAuthRefresh: true,
      validateStatus: () => true,
    });
    return response.status === 200 ? response.data ?? null : null;
  } catch {
    return null;
  }
}
