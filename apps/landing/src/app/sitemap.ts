import type { MetadataRoute } from 'next';
import { publicLegalPages, publicSiteUrls } from '@/lib/legal-document';

export default function sitemap(): MetadataRoute.Sitemap {
  const site = publicSiteUrls();
  const pages = ['/', ...publicLegalPages.map((page) => page.path)];
  return pages.map((page) => ({
    url: `${site.landing}${page}`,
    changeFrequency: 'monthly',
    priority: page === '/' ? 1 : 0.6,
  }));
}
