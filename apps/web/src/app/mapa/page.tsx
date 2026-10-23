import type { Metadata } from 'next';
import { MapPlaceholder } from '@/components/map-placeholder';

export const metadata: Metadata = { title: 'Mapa' };

export default function MapPage() {
  return <MapPlaceholder />;
}
