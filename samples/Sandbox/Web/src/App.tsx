import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { MapView, type MapViewHandle } from './components/MapView';
import { DownloadPanel } from './components/DownloadPanel';
import { ExtractPanel } from './components/ExtractPanel';
import { panelStyle } from './components/styles';
import { fetchVersion, fetchRoadNetwork, fetchCacheDir, type VersionResponse, type ExtractCompleteEvent, type StatsResponse } from './api/client';
import { WEB_VERSION } from './version';

export function App() {
  const mapRef = useRef<MapViewHandle>(null);
  const [version, setVersion] = useState<VersionResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [pbfPath, setPbfPath] = useState<string | null>(null);
  const [bbox, setBbox] = useState<[number, number, number, number] | null>(null);
  const [graphLoaded, setGraphLoaded] = useState(false);
  const [cacheDir, setCacheDir] = useState('');

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

  async function handleExtracted(_result: ExtractCompleteEvent) {
    await loadRoadNetwork();
  }

  async function handleLoaded(stats: StatsResponse) {
    await loadRoadNetwork(stats);
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
          onExtracted={handleExtracted}
          onLoaded={handleLoaded}
          cacheDir={cacheDir}
        />

        {graphLoaded && (
          <div style={panelStyle}>
            <div style={{ fontSize: 12, color: '#059669' }}>
              Graph loaded — road network displayed on map
            </div>
          </div>
        )}
      </div>
      <div style={mapAreaStyle}>
        <MapView ref={mapRef} onBboxDrawn={handleBboxDrawn} />
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
