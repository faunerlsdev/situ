import type { MapPerson } from '@situ/shared';

export type PersonMarker =
  | { kind: 'person'; person: MapPerson }
  | { kind: 'cluster'; key: string; latitude: number; longitude: number; count: number };

export function seenLabel(live: boolean, lastSeenAt: string | null, nowMs: number): string {
  if (live) {
    return 'Ahora';
  }
  if (!lastSeenAt) {
    return 'Sin señal reciente';
  }
  const minutes = Math.max(0, Math.floor((nowMs - Date.parse(lastSeenAt)) / 60_000));
  if (!Number.isFinite(minutes)) {
    return 'Sin señal reciente';
  }
  if (minutes < 1) {
    return 'Hace un momento';
  }
  if (minutes < 60) {
    return `Hace ${minutes} min`;
  }
  const hours = Math.floor(minutes / 60);
  return hours === 1 ? 'Hace 1 h' : `Hace ${hours} h`;
}

export function emitIntervalSeconds(config: object | null | undefined): number {
  if (!config || !('locationEmitIntervalSeconds' in config)) {
    return 20;
  }
  const value = Number((config as { locationEmitIntervalSeconds?: number }).locationEmitIntervalSeconds);
  return Number.isFinite(value) && value >= 10 ? value : 20;
}

/** Friend pins replace the coarser public pin of the same person. The viewer's own pin is never drawn. */
export function mergePeople(friends: readonly MapPerson[], publics: readonly MapPerson[], selfId: string | null): MapPerson[] {
  const byId = new Map<string, MapPerson>();
  for (const person of publics) {
    if (person.userId !== selfId) {
      byId.set(person.userId, person);
    }
  }
  for (const person of friends) {
    if (person.userId !== selfId) {
      byId.set(person.userId, person);
    }
  }
  return [...byId.values()];
}

export function clusterPeople(people: readonly MapPerson[], latitudeDelta: number): PersonMarker[] {
  if (latitudeDelta <= 0.15) {
    return people.map((person) => ({ kind: 'person', person }));
  }
  const groups = new Map<string, MapPerson[]>();
  for (const person of people) {
    const key = `${Math.round(person.latitude / 0.05)}:${Math.round(person.longitude / 0.05)}`;
    const group = groups.get(key) ?? [];
    group.push(person);
    groups.set(key, group);
  }
  return [...groups.entries()].map(([key, group]) => {
    if (group.length === 1) {
      return { kind: 'person' as const, person: group[0]! };
    }
    const latitude = group.reduce((sum, person) => sum + person.latitude, 0) / group.length;
    const longitude = group.reduce((sum, person) => sum + person.longitude, 0) / group.length;
    return { kind: 'cluster' as const, key, latitude, longitude, count: group.length };
  });
}
