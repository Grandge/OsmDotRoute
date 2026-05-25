import maplibregl, { type LngLatBoundsLike, type MapMouseEvent } from 'maplibre-gl';
import { useEffect, useImperativeHandle, useRef, forwardRef } from 'react';
import 'maplibre-gl/dist/maplibre-gl.css';

export interface MapViewHandle {
  fitBounds(bounds: LngLatBoundsLike, padding?: number): void;
  setRoadNetwork(geojson: GeoJSON.FeatureCollection | null): void;
  setOdrgRoadNetwork(geojson: GeoJSON.FeatureCollection | null): void;
  setMeshGrid(geojson: GeoJSON.FeatureCollection | null): void;
  setRestrictions(geojson: GeoJSON.FeatureCollection | null): void;
  setRoute(line: GeoJSON.LineString | null): void;
  setRouteEndpoints(points: { from?: [number, number]; to?: [number, number] }): void;
  setPolygonDraft(vertices: [number, number][]): void;
  getMap(): maplibregl.Map | null;
}

interface Props {
  initialCenter?: [number, number];
  initialZoom?: number;
  onBoundsChange?: (sw: [number, number], ne: [number, number]) => void;
  onMapClick?: (lngLat: { lng: number; lat: number }, feature: maplibregl.MapGeoJSONFeature | null) => void;
}

const ROAD_SOURCE = 'road-network';
const ROAD_LAYER = 'road-network-line';
const ODRG_ROAD_SOURCE = 'odrg-road-network';
const ODRG_ROAD_LAYER = 'odrg-road-network-line';
const MESH_SOURCE = 'mesh-grid';
const MESH_LAYER_FILL = 'mesh-grid-fill';
const MESH_LAYER_LINE = 'mesh-grid-line';
const REST_SOURCE = 'restrictions';
const REST_LAYER_FILL = 'restrictions-fill';
const REST_LAYER_LINE = 'restrictions-line';
const ROUTE_SOURCE = 'route';
const ROUTE_LAYER = 'route-line';
const ENDPT_SOURCE = 'route-endpoints';
const ENDPT_LAYER = 'route-endpoints-circle';
const DRAFT_SOURCE = 'polygon-draft';
const DRAFT_LAYER_LINE = 'polygon-draft-line';
const DRAFT_LAYER_PT = 'polygon-draft-points';

export const MapView = forwardRef<MapViewHandle, Props>(function MapView(
  { initialCenter = [139.767, 35.681], initialZoom = 6, onBoundsChange, onMapClick },
  ref,
) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const onMapClickRef = useRef(onMapClick);
  onMapClickRef.current = onMapClick;

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: {
        version: 8,
        sources: {
          osm: {
            type: 'raster',
            tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
            tileSize: 256,
            attribution: '© OpenStreetMap contributors',
          },
        },
        layers: [{ id: 'osm', type: 'raster', source: 'osm' }],
      },
      center: initialCenter,
      zoom: initialZoom,
    });
    mapRef.current = map;

    const emitBounds = () => {
      const b = map.getBounds();
      onBoundsChange?.([b.getSouth(), b.getWest()], [b.getNorth(), b.getEast()]);
    };
    map.on('load', emitBounds);
    map.on('moveend', emitBounds);

    map.on('click', (e: MapMouseEvent) => {
      // メッシュレイヤーが最優先、次に制約レイヤー
      const layerIds = [MESH_LAYER_FILL, REST_LAYER_FILL].filter((id) => map.getLayer(id));
      const features = layerIds.length > 0 ? map.queryRenderedFeatures(e.point, { layers: layerIds }) : [];
      onMapClickRef.current?.(e.lngLat, features[0] ?? null);
    });

    return () => {
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useImperativeHandle(ref, () => ({
    fitBounds(bounds, padding = 32) {
      mapRef.current?.fitBounds(bounds, { padding, animate: false });
    },
    setRoadNetwork(geojson) {
      runWhenStyleReady((m) => updateGeoJsonSource(m, ROAD_SOURCE, geojson, () => {
        m.addLayer({
          id: ROAD_LAYER,
          type: 'line',
          source: ROAD_SOURCE,
          paint: { 'line-color': '#1e6fff', 'line-width': 1.2, 'line-opacity': 0.55 },
        });
      }, [ROAD_LAYER]));
    },
    setOdrgRoadNetwork(geojson) {
      runWhenStyleReady((m) => updateGeoJsonSource(m, ODRG_ROAD_SOURCE, geojson, () => {
        // RouterDb (青) と区別するため赤系、僅かに太く + dash で重ね表示時に視認性確保
        m.addLayer({
          id: ODRG_ROAD_LAYER,
          type: 'line',
          source: ODRG_ROAD_SOURCE,
          paint: {
            'line-color': '#dc2626',
            'line-width': 1.4,
            'line-opacity': 0.6,
            'line-dasharray': [3, 2],
          },
        });
      }, [ODRG_ROAD_LAYER]));
    },
    setMeshGrid(geojson) {
      runWhenStyleReady((m) => updateGeoJsonSource(m, MESH_SOURCE, geojson, () => {
        m.addLayer({
          id: MESH_LAYER_FILL,
          type: 'fill',
          source: MESH_SOURCE,
          paint: { 'fill-color': '#9ca3af', 'fill-opacity': 0.06 },
        });
        m.addLayer({
          id: MESH_LAYER_LINE,
          type: 'line',
          source: MESH_SOURCE,
          paint: { 'line-color': '#4b5563', 'line-width': 0.6, 'line-opacity': 0.7 },
        });
      }, [MESH_LAYER_LINE, MESH_LAYER_FILL]));
    },
    setRestrictions(geojson) {
      runWhenStyleReady((m) => updateGeoJsonSource(m, REST_SOURCE, geojson, () => {
        m.addLayer({
          id: REST_LAYER_FILL,
          type: 'fill',
          source: REST_SOURCE,
          paint: {
            'fill-color': [
              'match', ['get', 'kind'],
              'block', '#dc2626',
              'difficulty', '#f59e0b',
              '#6b7280',
            ],
            'fill-opacity': 0.35,
          },
        });
        m.addLayer({
          id: REST_LAYER_LINE,
          type: 'line',
          source: REST_SOURCE,
          paint: {
            'line-color': [
              'match', ['get', 'kind'],
              'block', '#7f1d1d',
              'difficulty', '#92400e',
              '#374151',
            ],
            'line-width': 1.4,
          },
        });
      }, [REST_LAYER_LINE, REST_LAYER_FILL]));
    },
    setRoute(line) {
      const fc: GeoJSON.FeatureCollection | null = line
        ? { type: 'FeatureCollection', features: [{ type: 'Feature', properties: {}, geometry: line }] }
        : null;
      runWhenStyleReady((m) => updateGeoJsonSource(m, ROUTE_SOURCE, fc, () => {
        m.addLayer({
          id: ROUTE_LAYER,
          type: 'line',
          source: ROUTE_SOURCE,
          paint: { 'line-color': '#16a34a', 'line-width': 4, 'line-opacity': 0.9 },
        });
      }, [ROUTE_LAYER]));
    },
    setRouteEndpoints(points) {
      const feats: GeoJSON.Feature[] = [];
      if (points.from) feats.push(makePoint(points.from, 'from'));
      if (points.to) feats.push(makePoint(points.to, 'to'));
      const fc: GeoJSON.FeatureCollection = { type: 'FeatureCollection', features: feats };
      runWhenStyleReady((m) => updateGeoJsonSource(m, ENDPT_SOURCE, fc, () => {
        m.addLayer({
          id: ENDPT_LAYER,
          type: 'circle',
          source: ENDPT_SOURCE,
          paint: {
            'circle-radius': 8,
            'circle-color': ['match', ['get', 'role'], 'from', '#059669', 'to', '#dc2626', '#6b7280'],
            'circle-stroke-color': '#fff',
            'circle-stroke-width': 2,
          },
        });
      }, [ENDPT_LAYER]));
    },
    setPolygonDraft(vertices) {
      const lineFC: GeoJSON.FeatureCollection = {
        type: 'FeatureCollection',
        features: vertices.length >= 2
          ? [{ type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: vertices } }]
          : [],
      };
      const pointFC: GeoJSON.FeatureCollection = {
        type: 'FeatureCollection',
        features: vertices.map((v) => ({ type: 'Feature', properties: {}, geometry: { type: 'Point', coordinates: v } })),
      };
      runWhenStyleReady((m) => {
        updateGeoJsonSource(m, DRAFT_SOURCE, lineFC, () => {
          m.addLayer({
            id: DRAFT_LAYER_LINE,
            type: 'line',
            source: DRAFT_SOURCE,
            paint: { 'line-color': '#7c3aed', 'line-width': 2, 'line-dasharray': [2, 2] },
          });
        }, [DRAFT_LAYER_LINE]);
        updateGeoJsonSource(m, DRAFT_SOURCE + '-pt', pointFC, () => {
          m.addLayer({
            id: DRAFT_LAYER_PT,
            type: 'circle',
            source: DRAFT_SOURCE + '-pt',
            paint: { 'circle-radius': 5, 'circle-color': '#7c3aed', 'circle-stroke-color': '#fff', 'circle-stroke-width': 2 },
          });
        }, [DRAFT_LAYER_PT]);
      });
    },
    getMap() {
      return mapRef.current;
    },
  }));

  return <div ref={containerRef} style={{ width: '100%', height: '100%' }} />;

  function runWhenStyleReady(fn: (m: maplibregl.Map) => void) {
    const m = mapRef.current;
    if (!m) return;
    if (m.isStyleLoaded()) {
      fn(m);
    } else {
      m.once('load', () => fn(m));
    }
  }
});

function updateGeoJsonSource(
  map: maplibregl.Map,
  sourceId: string,
  data: GeoJSON.FeatureCollection | null,
  addLayers: () => void,
  layerIds: string[],
) {
  const existing = map.getSource(sourceId) as maplibregl.GeoJSONSource | undefined;
  const empty = data === null || (data.type === 'FeatureCollection' && data.features.length === 0);
  if (empty) {
    for (const id of layerIds) {
      if (map.getLayer(id)) map.removeLayer(id);
    }
    if (existing) map.removeSource(sourceId);
    return;
  }
  if (existing) {
    existing.setData(data);
    return;
  }
  map.addSource(sourceId, { type: 'geojson', data });
  addLayers();
}

function makePoint(latlng: [number, number], role: 'from' | 'to'): GeoJSON.Feature {
  // 入力は [lat, lon] 順、GeoJSON は [lon, lat]
  return {
    type: 'Feature',
    properties: { role },
    geometry: { type: 'Point', coordinates: [latlng[1], latlng[0]] },
  };
}
