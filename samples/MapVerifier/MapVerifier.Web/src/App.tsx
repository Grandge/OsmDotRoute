import { useCallback, useEffect, useRef, useState } from 'react';
import type { LngLatBoundsLike, MapGeoJSONFeature } from 'maplibre-gl';
import { MapView, type MapViewHandle } from './components/MapView';
import { VersionBanner } from './components/VersionBanner';
import { LoadPanel } from './components/LoadPanel';
import { LoadOdrgPanel } from './components/LoadOdrgPanel';
import { MapBoundsPanel } from './components/MapBoundsPanel';
import { MeshGridPanel } from './components/MeshGridPanel';
import { PolygonEditorPanel } from './components/PolygonEditorPanel';
import { RoutePanel } from './components/RoutePanel';
import { RestrictionListPanel } from './components/RestrictionListPanel';
import { GmlImportPanel } from './components/GmlImportPanel';
import { fetchRestrictionsGeoJson, type RouteResponse, type StatsResponse } from './api/client';

type PickMode = 'idle' | 'pickFrom' | 'pickTo';

export function App() {
  const mapRef = useRef<MapViewHandle | null>(null);
  const [bounds, setBounds] = useState<{ sw: [number, number]; ne: [number, number] } | null>(null);
  const [loaded, setLoaded] = useState(false);

  // 経路指定モード
  const [pickMode, setPickMode] = useState<PickMode>('idle');
  const [from, setFrom] = useState<[number, number] | null>(null);
  const [to, setTo] = useState<[number, number] | null>(null);

  // ポリゴン描画
  const [drawing, setDrawing] = useState(false);
  const [vertices, setVertices] = useState<[number, number][]>([]);

  // メッシュ選択
  const [selectedMeshCode, setSelectedMeshCode] = useState<number | null>(null);

  // 制約一覧再読込トリガ
  const [restrictionsNonce, setRestrictionsNonce] = useState(0);

  // RouterDb 読込: マップをそのデータ範囲にフィットする (ユーザー要望)
  const handleLoaded = useCallback((stats: StatsResponse) => {
    const fit: LngLatBoundsLike = [
      [stats.southWest.longitude, stats.southWest.latitude],
      [stats.northEast.longitude, stats.northEast.latitude],
    ];
    mapRef.current?.fitBounds(fit);
    setLoaded(true);
  }, []);

  const handleRoadNetwork = useCallback((geojson: GeoJSON.FeatureCollection) => {
    mapRef.current?.setRoadNetwork(geojson.features.length === 0 ? null : geojson);
  }, []);

  // .odrg 読込: RouterDb と独立 (fit はしない、RouterDb と重ね表示前提)
  const handleOdrgRoadNetwork = useCallback((geojson: GeoJSON.FeatureCollection) => {
    mapRef.current?.setOdrgRoadNetwork(geojson.features.length === 0 ? null : geojson);
  }, []);

  const handleApplyBounds = useCallback((sw: [number, number], ne: [number, number]) => {
    mapRef.current?.fitBounds([[sw[1], sw[0]], [ne[1], ne[0]]]);
  }, []);

  const refreshRestrictions = useCallback(async () => {
    try {
      const fc = await fetchRestrictionsGeoJson();
      mapRef.current?.setRestrictions(fc.features.length === 0 ? null : fc);
    } catch {
      mapRef.current?.setRestrictions(null);
    }
    setRestrictionsNonce((n) => n + 1);
  }, []);

  // 制約レイヤーの初期表示と変更追従
  useEffect(() => {
    if (!loaded) return;
    void refreshRestrictions();
  }, [loaded, refreshRestrictions]);

  // 描画ドラフトをマップに反映
  useEffect(() => {
    mapRef.current?.setPolygonDraft(drawing ? vertices : []);
  }, [drawing, vertices]);

  // 起終点マーカーをマップに反映
  useEffect(() => {
    mapRef.current?.setRouteEndpoints({
      from: from ?? undefined,
      to: to ?? undefined,
    });
  }, [from, to]);

  // マップクリック振り分け
  const handleMapClick = useCallback(
    (lngLat: { lng: number; lat: number }, feature: MapGeoJSONFeature | null) => {
      // 1. 経路ピック中
      if (pickMode === 'pickFrom') {
        setFrom([lngLat.lat, lngLat.lng]);
        setPickMode('idle');
        return;
      }
      if (pickMode === 'pickTo') {
        setTo([lngLat.lat, lngLat.lng]);
        setPickMode('idle');
        return;
      }
      // 2. ポリゴン描画中
      if (drawing) {
        setVertices((v) => [...v, [lngLat.lng, lngLat.lat]]);
        return;
      }
      // 3. メッシュクリック (feature の properties.meshCode を見る)
      const meshCode = feature?.properties?.meshCode;
      if (meshCode !== undefined && meshCode !== null) {
        const n = typeof meshCode === 'number' ? meshCode : Number(meshCode);
        if (Number.isFinite(n)) {
          setSelectedMeshCode(n);
        }
      }
    },
    [pickMode, drawing],
  );

  const handleRouteCalculated = useCallback((r: RouteResponse | null) => {
    mapRef.current?.setRoute(r?.geometry ?? null);
  }, []);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
      <VersionBanner />
      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
        <aside style={asideStyle}>
          <LoadPanel onLoaded={handleLoaded} onRoadNetworkFetched={handleRoadNetwork} />
          <LoadOdrgPanel onLoaded={() => { /* fit はしない */ }} onRoadNetworkFetched={handleOdrgRoadNetwork} />
          <MapBoundsPanel currentBounds={bounds} onApply={handleApplyBounds} />
          <RoutePanel
            from={from}
            to={to}
            pickMode={pickMode}
            onSetPickMode={setPickMode}
            onSetFrom={setFrom}
            onSetTo={setTo}
            onRouteCalculated={handleRouteCalculated}
          />
          <MeshGridPanel
            currentBounds={bounds}
            selectedMeshCode={selectedMeshCode}
            onMeshGridFetched={(g) => mapRef.current?.setMeshGrid(g)}
            onMeshRegistered={() => { void refreshRestrictions(); }}
            onClearSelection={() => setSelectedMeshCode(null)}
          />
          <PolygonEditorPanel
            drawing={drawing}
            vertices={vertices}
            onStartDrawing={() => { setDrawing(true); setVertices([]); }}
            onCancelDrawing={() => { setDrawing(false); setVertices([]); }}
            onUndoVertex={() => setVertices((v) => v.slice(0, -1))}
            onPolygonRegistered={() => {
              setDrawing(false);
              setVertices([]);
              void refreshRestrictions();
            }}
          />
          <GmlImportPanel
            currentBounds={bounds}
            onImported={() => { void refreshRestrictions(); }}
          />
          <RestrictionListPanel
            refreshNonce={restrictionsNonce}
            onChanged={() => { void refreshRestrictions(); }}
          />
        </aside>
        <main style={{ flex: 1, position: 'relative' }}>
          <MapView
            ref={mapRef}
            onBoundsChange={(sw, ne) => setBounds({ sw, ne })}
            onMapClick={handleMapClick}
          />
          {pickMode !== 'idle' && (
            <div style={overlayStyle}>マップ上の場所をクリックして {pickMode === 'pickFrom' ? '起点' : '終点'} を指定</div>
          )}
          {drawing && (
            <div style={overlayStyle}>マップをクリックして頂点追加 (現在 {vertices.length} 頂点)</div>
          )}
        </main>
      </div>
    </div>
  );
}

const asideStyle: React.CSSProperties = {
  width: 360,
  padding: 12,
  overflowY: 'auto',
  background: '#fff',
  borderRight: '1px solid #d1d5db',
};

const overlayStyle: React.CSSProperties = {
  position: 'absolute',
  top: 8,
  left: 8,
  padding: '6px 10px',
  background: 'rgba(31,41,55,0.85)',
  color: '#fff',
  borderRadius: 4,
  fontSize: 13,
  fontFamily: 'system-ui, sans-serif',
  zIndex: 1,
};
