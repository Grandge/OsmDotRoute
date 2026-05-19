export interface VersionResponse {
  name: string;
  version: string;
}

export interface CoordinateDto {
  latitude: number;
  longitude: number;
}

export interface StatsResponse {
  vertexCount: number;
  edgeCount: number;
  southWest: CoordinateDto;
  northEast: CoordinateDto;
}

export interface ErrorResponse {
  error: string;
  message: string;
}

export type Kind = 'block' | 'difficulty';

export interface RestrictionItem {
  id: string;
  kind: Kind;
  difficultyType: string | null;
  shapeType: 'polygon' | 'mesh';
  outerBoundary: CoordinateDto[] | null;
  meshCodes: number[] | null;
  tag: string | null;
}

export interface RouteResponse {
  found: boolean;
  distanceM: number;
  durationSec: number;
  geometry: GeoJSON.LineString | null;
}

async function handle<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let detail: ErrorResponse | undefined;
    try {
      detail = (await res.json()) as ErrorResponse;
    } catch {
      // ignore
    }
    throw new Error(detail?.message ?? `HTTP ${res.status} ${res.statusText}`);
  }
  return (await res.json()) as T;
}

async function ensureOk(res: Response): Promise<void> {
  if (!res.ok) {
    let detail: ErrorResponse | undefined;
    try {
      detail = (await res.json()) as ErrorResponse;
    } catch {
      // ignore
    }
    throw new Error(detail?.message ?? `HTTP ${res.status} ${res.statusText}`);
  }
}

export const fetchVersion = async () => handle<VersionResponse>(await fetch('/api/version'));

export const loadRouterDb = async (routerDbPath: string) =>
  handle<StatsResponse>(
    await fetch('/api/load', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ routerDbPath }),
    }),
  );

export const fetchStats = async () => handle<StatsResponse>(await fetch('/api/stats'));

export async function fetchRoadNetwork(): Promise<GeoJSON.FeatureCollection> {
  const res = await fetch('/api/road-network');
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as GeoJSON.FeatureCollection;
}

export async function fetchMeshGrid(
  sw: [number, number],
  ne: [number, number],
  level: '1km' | '500m' | '250m',
): Promise<GeoJSON.FeatureCollection> {
  const qs = new URLSearchParams({
    swLat: sw[0].toString(),
    swLon: sw[1].toString(),
    neLat: ne[0].toString(),
    neLon: ne[1].toString(),
    level,
  });
  const res = await fetch(`/api/mesh/grid?${qs}`);
  if (!res.ok) {
    let detail: ErrorResponse | undefined;
    try {
      detail = (await res.json()) as ErrorResponse;
    } catch {
      // ignore
    }
    throw new Error(detail?.message ?? `HTTP ${res.status}`);
  }
  return (await res.json()) as GeoJSON.FeatureCollection;
}

export async function registerPolygonRestriction(req: {
  kind: Kind;
  difficultyType?: string;
  outerBoundary: CoordinateDto[];
  tag?: string;
}): Promise<{ id: string }> {
  return handle<{ id: string }>(
    await fetch('/api/restrictions/polygon', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  );
}

export async function registerMeshRestriction(req: {
  kind: Kind;
  difficultyType?: string;
  meshCodes: number[];
  tag?: string;
}): Promise<{ id: string }> {
  return handle<{ id: string }>(
    await fetch('/api/restrictions/mesh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  );
}

export async function listRestrictions(): Promise<{ items: RestrictionItem[] }> {
  return handle<{ items: RestrictionItem[] }>(await fetch('/api/restrictions'));
}

export async function fetchRestrictionsGeoJson(): Promise<GeoJSON.FeatureCollection> {
  const res = await fetch('/api/restrictions/geojson');
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as GeoJSON.FeatureCollection;
}

export async function deleteRestriction(id: string): Promise<void> {
  await ensureOk(await fetch(`/api/restrictions/${id}`, { method: 'DELETE' }));
}

export async function clearAllRestrictions(): Promise<void> {
  await ensureOk(await fetch('/api/restrictions', { method: 'DELETE' }));
}

export async function calculateRoute(req: {
  fromLat: number;
  fromLon: number;
  toLat: number;
  toLon: number;
  profile: 'car' | 'pedestrian';
}): Promise<RouteResponse> {
  return handle<RouteResponse>(
    await fetch('/api/route', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  );
}

export interface BrowseResult {
  currentPath: string;
  parentPath: string | null;
  directories: { name: string }[];
  files: { name: string; size: number }[];
  drives: string[];
}

export async function browseDirectory(path: string | null, pattern: string | null): Promise<BrowseResult> {
  const qs = new URLSearchParams();
  if (path) qs.set('path', path);
  if (pattern) qs.set('pattern', pattern);
  return handle<BrowseResult>(await fetch(`/api/files/browse?${qs}`));
}

export interface GmlImportRequest {
  filePath: string;
  kind: Kind;
  difficultyType?: string;
  useMapBounds: boolean;
  mapBoundsSouthWest?: CoordinateDto;
  mapBoundsNorthEast?: CoordinateDto;
  tag?: string;
}

export interface GmlImportResponse {
  ids: string[];
  acceptedCount: number;
}

export async function importGmlFile(req: GmlImportRequest): Promise<GmlImportResponse> {
  return handle<GmlImportResponse>(
    await fetch('/api/restrictions/gml-file', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  );
}
