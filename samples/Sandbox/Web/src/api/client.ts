export interface VersionResponse {
  name: string;
  version: string;
}

export interface ErrorResponse {
  error: string;
  message: string;
}

export interface RegionInfo {
  key: string;
  displayName: string;
  description: string;
}

export interface CachedPbfInfo {
  regionKey: string;
  displayName: string;
  sizeBytes: number;
  lastModifiedUtc: string;
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

export const fetchVersion = async () => handle<VersionResponse>(await fetch('/api/version'));

export const fetchRegions = async () => handle<RegionInfo[]>(await fetch('/api/regions'));

export const fetchCacheStatus = async () =>
  handle<{ items: CachedPbfInfo[] }>(await fetch('/api/cache/status'));

export const fetchCacheDir = async () =>
  handle<{ path: string }>(await fetch('/api/cache/dir'));

export async function setCacheDir(path: string): Promise<{ path: string }> {
  return handle<{ path: string }>(
    await fetch('/api/cache/dir', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path }),
    }),
  );
}

export async function downloadPbf(
  region: string,
  onProgress: (downloaded: number, total: number) => void,
): Promise<{ path: string; sizeBytes: number }> {
  const res = await fetch('/api/download', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ region }),
  });

  if (res.status === 409) {
    throw new Error('Another download is already in progress');
  }

  if (!res.ok && res.headers.get('content-type')?.includes('application/json')) {
    const err = (await res.json()) as ErrorResponse;
    throw new Error(err.message);
  }

  const reader = res.body!.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let result: { path: string; sizeBytes: number } | null = null;

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });

    const lines = buffer.split('\n');
    buffer = lines.pop()!;

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue;
      const data = JSON.parse(line.slice(6));

      if (data.type === 'progress') {
        onProgress(data.downloaded, data.total);
      } else if (data.type === 'complete' || data.type === 'cached') {
        result = { path: data.path, sizeBytes: data.sizeBytes };
        onProgress(data.sizeBytes, data.sizeBytes);
      } else if (data.type === 'error') {
        throw new Error(data.message);
      }
    }
  }

  if (!result) throw new Error('Download stream ended without completion');
  return result;
}

export async function deleteCachedPbf(region: string): Promise<void> {
  const res = await fetch(`/api/cache/${region}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 404) {
    throw new Error(`HTTP ${res.status}`);
  }
}

// --- Browse ---

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

// --- Load ---

export async function loadOdrg(odrgPath: string): Promise<StatsResponse> {
  return handle<StatsResponse>(
    await fetch('/api/load', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ odrgPath }),
    }),
  );
}

// --- Extract ---

export interface ExtractPhaseEvent {
  type: 'phase';
  phase: string;
  message: string;
}

export interface ExtractCompleteEvent {
  type: 'complete';
  odrgPath: string;
  vertexCount: number;
  edgeCount: number;
  fileSizeBytes: number;
  extractSeconds: number;
}

export async function extractOdrg(
  pbfPath: string,
  bbox: [number, number, number, number],
  profiles: string[],
  onPhase: (phase: string, message: string) => void,
): Promise<ExtractCompleteEvent> {
  const res = await fetch('/api/extract', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ pbfPath, bbox, profiles }),
  });

  if (res.status === 409) {
    throw new Error('Another extraction is already in progress');
  }

  if (!res.ok && res.headers.get('content-type')?.includes('application/json')) {
    const err = (await res.json()) as ErrorResponse;
    throw new Error(err.message);
  }

  const reader = res.body!.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let result: ExtractCompleteEvent | null = null;

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });

    const lines = buffer.split('\n');
    buffer = lines.pop()!;

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue;
      const data = JSON.parse(line.slice(6));

      if (data.type === 'phase') {
        onPhase(data.phase, data.message);
      } else if (data.type === 'complete') {
        result = data as ExtractCompleteEvent;
      } else if (data.type === 'error') {
        throw new Error(data.message);
      }
    }
  }

  if (!result) throw new Error('Extract stream ended without completion');
  return result;
}

// --- Graph ---

export interface StatsResponse {
  vertexCount: number;
  edgeCount: number;
  southWest: { latitude: number; longitude: number };
  northEast: { latitude: number; longitude: number };
  profileNames: string[];
}

export const fetchGraphStats = async () => handle<StatsResponse>(await fetch('/api/graph/stats'));

export async function fetchRoadNetwork(): Promise<GeoJSON.FeatureCollection> {
  const res = await fetch('/api/road-network');
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as GeoJSON.FeatureCollection;
}
