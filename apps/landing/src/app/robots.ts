import type { MetadataRoute } from 'next';
import { publicSiteUrls } from '@/lib/legal-document';

export default function robots(): MetadataRoute.Robots {
  const site = publicSiteUrls();
  return {
    rules: { userAgent: '*', allow: '/' },
    sitemap: `${site.landing}/sitemap.xml`,
  };
}
