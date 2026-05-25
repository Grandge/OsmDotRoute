import { useState } from 'react';
import { calculateRoute, type GraphSource, type RouteResponse } from '../api/client';
import { panelStyle, h2Style, btnStyle, inputStyle, errorStyle } from './styles';

type PickMode = 'idle' | 'pickFrom' | 'pickTo';

interface Props {
  from: [number, number] | null; // [lat, lon]
  to: [number, number] | null;
  pickMode: PickMode;
  onSetPickMode: (mode: PickMode) => void;
  onSetFrom: (pt: [number, number] | null) => void;
  onSetTo: (pt: [number, number] | null) => void;
  onRouteCalculated: (route: RouteResponse | null) => void;
}

export function RoutePanel({ from, to, pickMode, onSetPickMode, onSetFrom, onSetTo, onRouteCalculated }: Props) {
  const [profile, setProfile] = useState<'car' | 'pedestrian'>('car');
  const [graphSource, setGraphSource] = useState<GraphSource>('routerdb');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<RouteResponse | null>(null);

  async function handleCalc() {
    if (!from || !to) {
      setError('起点と終点を指定してください。');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const r = await calculateRoute({
        fromLat: from[0], fromLon: from[1],
        toLat: to[0], toLon: to[1],
        profile,
        graphSource,
      });
      setLastResult(r);
      onRouteCalculated(r);
      if (!r.found) setError('経路が見つかりませんでした (REQ-RTE-006/008)');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function clearRoute() {
    setLastResult(null);
    onRouteCalculated(null);
    setError(null);
  }

  return (
    <section style={panelStyle}>
      <h2 style={h2Style}>経路計算</h2>
      <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: '4px 6px', fontSize: 13, alignItems: 'center' }}>
        <label>起点</label>
        <input
          style={inputStyle}
          value={from ? `${from[0].toFixed(5)}, ${from[1].toFixed(5)}` : ''}
          placeholder="緯度, 経度"
          onChange={(e) => {
            const [a, b] = e.target.value.split(',').map((s) => parseFloat(s.trim()));
            if (!Number.isNaN(a) && !Number.isNaN(b)) onSetFrom([a, b]);
          }}
        />
        <button
          style={{ ...btnStyle, background: pickMode === 'pickFrom' ? '#fde68a' : undefined }}
          onClick={() => onSetPickMode(pickMode === 'pickFrom' ? 'idle' : 'pickFrom')}
        >
          {pickMode === 'pickFrom' ? '取消' : 'マップで指定'}
        </button>

        <label>終点</label>
        <input
          style={inputStyle}
          value={to ? `${to[0].toFixed(5)}, ${to[1].toFixed(5)}` : ''}
          placeholder="緯度, 経度"
          onChange={(e) => {
            const [a, b] = e.target.value.split(',').map((s) => parseFloat(s.trim()));
            if (!Number.isNaN(a) && !Number.isNaN(b)) onSetTo([a, b]);
          }}
        />
        <button
          style={{ ...btnStyle, background: pickMode === 'pickTo' ? '#fde68a' : undefined }}
          onClick={() => onSetPickMode(pickMode === 'pickTo' ? 'idle' : 'pickTo')}
        >
          {pickMode === 'pickTo' ? '取消' : 'マップで指定'}
        </button>

        <label>プロファイル</label>
        <select value={profile} onChange={(e) => setProfile(e.target.value as 'car' | 'pedestrian')} style={inputStyle}>
          <option value="car">car</option>
          <option value="pedestrian">pedestrian</option>
        </select>
        <span />

        <label>グラフ</label>
        <select value={graphSource} onChange={(e) => setGraphSource(e.target.value as GraphSource)} style={inputStyle}>
          <option value="routerdb">routerdb (Phase 1)</option>
          <option value="odrg">.odrg (Phase 3 で対応予定)</option>
        </select>
        <span />
      </div>
      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <button onClick={handleCalc} disabled={busy} style={btnStyle}>
          {busy ? '計算中…' : '経路計算'}
        </button>
        <button onClick={clearRoute} disabled={!lastResult && !error} style={btnStyle}>ルートをクリア</button>
      </div>
      {lastResult?.found && (
        <p style={{ margin: '6px 0 0', fontSize: 13 }}>
          距離 <strong>{lastResult.distanceM.toFixed(0)}</strong> m / 所要時間 <strong>{lastResult.durationSec.toFixed(0)}</strong> s
        </p>
      )}
      {error && <p style={errorStyle}>{error}</p>}
    </section>
  );
}
