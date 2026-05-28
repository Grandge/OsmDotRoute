import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { MapView, type MapViewHandle } from './components/MapView';
import { DownloadPanel } from './components/DownloadPanel';
import { ExtractPanel } from './components/ExtractPanel';
import { RoutePanel, type PickMode } from './components/RoutePanel';
import { panelStyle } from './components/styles';
import { fetchVersion, fetchRoadNetwork, fetchCacheDir, type VersionResponse, type ExtractCompleteEvent, type StatsResponse, type RouteResponse } from './api/client';
import { WEB_VERSION } from './version';

export function App() {
  const mapRef = useRef<MapViewHandle>(null);
  const [version, setVersion] = useState<VersionResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [pbfPath, setPbfPath] = useState<string | null>(null);
  const [bbox, setBbox] = useState<[number, number, number, number] | null>(null);
  const [graphLoaded, setGraphLoaded] = useState(false);
  const [cacheDir, setCacheDir] = useState('');
  const [availableProfiles, setAvailableProfiles] = useState<string[]>([]);

  const [pickMode, setPickMode] = useState<PickMode>('idle');
  const [from, setFrom] = useState<[number, number] | null>(null);
  const [to, setTo] = useState<[number, number] | null>(null);

  useEffect(() => {
    fetchVersion().then(setVersion).catch((e: Error) => setError(e.message));
    fetchCacheDir().then((r) => setCacheDir(r.path)).catch(() => {});
  }, []);

  function handlePbfReady(_regionKey: string, path: string) {
    setPbfPath(path);
  }

  function handleStartBboxDraw() {
    mapRef.current?.startBboxDraw();
  }

  function handleBboxDrawn(newBbox: [number, number, number, number]) {
    setBbox(newBbox);
    mapRef.current?.setBboxRect(newBbox);
  }

  function handleClearBbox() {
    setBbox(null);
    mapRef.current?.setBboxRect(null);
    mapRef.current?.setRoadNetwork(null);
    mapRef.current?.setRoute(null);
    mapRef.current?.setRouteEndpoints({});
    mapRef.current?.setMeshGrid(null);
    mapRef.current?.setRestrictions(null);
    setGraphLoaded(false);
    setAvailableProfiles([]);
    setFrom(null);
    setTo(null);
    setPickMode('idle');
  }

  function handleBboxManualChange(newBbox: [number, number, number, number]) {
    setBbox(newBbox);
    mapRef.current?.setBboxRect(newBbox);
  }

  async function loadRoadNetwork(bounds?: { southWest: { latitude: number; longitude: number }; northEast: { latitude: number; longitude: number } }) {
    setGraphLoaded(true);
    try {
      const geojson = await fetchRoadNetwork();
      mapRef.current?.setRoadNetwork(geojson);
      if (bounds) {
        mapRef.current?.fitBounds([
          [bounds.southWest.longitude, bounds.southWest.latitude],
          [bounds.northEast.longitude, bounds.northEast.latitude],
        ]);
      } else if (bbox) {
        mapRef.current?.fitBounds([[bbox[0], bbox[1]], [bbox[2], bbox[3]]]);
      }
    } catch {
      // Road network display is optional
    }
  }

  async function handleExtracted(result: ExtractCompleteEvent) {
    setAvailableProfiles(result.profileNames);
    await loadRoadNetwork();
  }

  async function handleLoaded(stats: StatsResponse) {
    setAvailableProfiles(stats.profileNames);
    const loadedBbox: [number, number, number, number] = [
      stats.southWest.longitude,
      stats.southWest.latitude,
      stats.northEast.longitude,
      stats.northEast.latitude,
    ];
    setBbox(loadedBbox);
    mapRef.current?.setBboxRect(loadedBbox);
    await loadRoadNetwork(stats);
  }

  function handleMapClick(lngLat: { lng: number; lat: number }) {
    if (pickMode === 'pickFrom') {
      const pt: [number, number] = [lngLat.lat, lngLat.lng];
      setFrom(pt);
      setPickMode('idle');
      mapRef.current?.setRouteEndpoints({ from: pt, to: to ?? undefined });
    } else if (pickMode === 'pickTo') {
      const pt: [number, number] = [lngLat.lat, lngLat.lng];
      setTo(pt);
      setPickMode('idle');
      mapRef.current?.setRouteEndpoints({ from: from ?? undefined, to: pt });
    }
  }

  function handleRouteResult(result: RouteResponse) {
    if (result.found && result.geometry) {
      mapRef.current?.setRoute(result.geometry);
    } else {
      mapRef.current?.setRoute(null);
    }
  }

  function handleClearRoute() {
    setFrom(null);
    setTo(null);
    setPickMode('idle');
    mapRef.current?.setRoute(null);
    mapRef.current?.setRouteEndpoints({});
  }

  return (
    <div style={rootStyle}>
      <div style={sidebarStyle}>
        <div style={panelStyle}>
          <h2 style={{ margin: 0, fontSize: 16 }}>OsmDotRoute Sandbox</h2>
          <div style={{ fontSize: 12, color: '#6b7280', marginTop: 4 }}>
            Web {WEB_VERSION}
            {version && <> / Server {version.version}</>}
            {error && <span style={{ color: '#dc2626' }}> (server error)</span>}
          </div>
        </div>

        <DownloadPanel
          onPbfReady={handlePbfReady}
          bbox={bbox}
          onStartBboxDraw={handleStartBboxDraw}
          onClearBbox={handleClearBbox}
          onBboxManualChange={handleBboxManualChange}
          onCacheDirChanged={setCacheDir}
        />

        <ExtractPanel
          pbfPath={pbfPath}
          bbox={bbox}
          availableProfiles={availableProfiles}
          onExtracted={handleExtracted}
          onLoaded={handleLoaded}
          cacheDir={cacheDir}
        />

        <RoutePanel
          graphLoaded={graphLoaded}
          availableProfiles={availableProfiles}
          from={from}
          to={to}
          pickMode={pickMode}
          onSetPickMode={setPickMode}
          onRouteResult={handleRouteResult}
          onClearRoute={handleClearRoute}
        />
      </div>
      <div style={mapAreaStyle}>
        <MapView
          ref={mapRef}
          onBboxDrawn={handleBboxDrawn}
          onMapClick={handleMapClick}
        />
      </div>
    </div>
  );
}

const rootStyle: CSSProperties = {
  display: 'flex',
  width: '100vw',
  height: '100vh',
  margin: 0,
  padding: 0,
  fontFamily: 'system-ui, -apple-system, sans-serif',
};

const sidebarStyle: CSSProperties = {
  width: 360,
  minWidth: 360,
  height: '100%',
  overflowY: 'auto',
  padding: 10,
  boxSizing: 'border-box',
  background: '#fff',
  borderRight: '1px solid #e5e7eb',
};

const mapAreaStyle: CSSProperties = {
  flex: 1,
  height: '100%',
};
