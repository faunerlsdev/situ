'use client';

import { APIProvider, Map, Marker, useMap } from '@vis.gl/react-google-maps';
import { useEffect } from 'react';

export type MapPin = {
  id: string;
  lat: number;
  lng: number;
  kind: 'point' | 'cluster';
  title: string;
  iconUrl?: string | null;
  count?: number;
};

export type MapViewport = {
  north: number;
  south: number;
  east: number;
  west: number;
  zoom: number;
};

const fallbackIcon = `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="40" viewBox="0 0 32 40"><path fill="#FF6400" d="M16 0C7.2 0 0 7.2 0 16c0 12 16 24 16 24s16-12 16-24C32 7.2 24.8 0 16 0z"/><circle fill="#1C1C1C" cx="16" cy="16" r="6"/></svg>')}`;

export function GoogleMapView({
  apiKey,
  center,
  zoom,
  pins,
  focus,
  onViewport,
  onSelect,
}: {
  apiKey: string;
  center: { lat: number; lng: number };
  zoom: number;
  pins: MapPin[];
  focus: { lat: number; lng: number; nonce: number } | null;
  onViewport: (view: MapViewport) => void;
  onSelect: (id: string) => void;
}) {
  return (
    <APIProvider apiKey={apiKey}>
      <Map
        defaultCenter={center}
        defaultZoom={zoom}
        gestureHandling="greedy"
        disableDefaultUI
        styles={darkStyle}
        style={{ width: '100%', height: '100%' }}
        onIdle={(event) => {
          const map = event.map;
          const bounds = map.getBounds();
          if (!bounds) {
            return;
          }
          const northEast = bounds.getNorthEast();
          const southWest = bounds.getSouthWest();
          onViewport({
            north: northEast.lat(),
            east: northEast.lng(),
            south: southWest.lat(),
            west: southWest.lng(),
            zoom: map.getZoom() ?? zoom,
          });
        }}
      >
        <FocusCamera focus={focus} />
        {pins.map((pin) => (
          <Marker
            key={pin.id}
            position={{ lat: pin.lat, lng: pin.lng }}
            title={pin.title}
            icon={pin.kind === 'point' ? (pin.iconUrl || fallbackIcon) : undefined}
            label={pin.kind === 'cluster' ? String(pin.count ?? 0) : undefined}
            onClick={() => {
              if (pin.kind === 'point') {
                onSelect(pin.id);
              }
            }}
          />
        ))}
      </Map>
    </APIProvider>
  );
}

function FocusCamera({ focus }: { focus: { lat: number; lng: number; nonce: number } | null }) {
  const map = useMap();
  useEffect(() => {
    if (!map || !focus) {
      return;
    }
    map.panTo({ lat: focus.lat, lng: focus.lng });
    map.setZoom(15);
  }, [focus, map]);
  return null;
}

const darkStyle = [
  { elementType: 'geometry', stylers: [{ color: '#1C1C1C' }] },
  { elementType: 'labels.text.fill', stylers: [{ color: '#A8A8A8' }] },
  { elementType: 'labels.text.stroke', stylers: [{ color: '#1C1C1C' }] },
  { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#17263C' }] },
  { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#2E2E2C' }] },
  { featureType: 'poi', elementType: 'geometry', stylers: [{ color: '#242424' }] },
  { featureType: 'poi', elementType: 'labels', stylers: [{ visibility: 'off' }] },
];
