import { useState } from 'react';
import { loadRouterDb, fetchRoadNetwork, type StatsResponse } from '../api/client';
import { panelStyle, h2Style, btnStyle, errorStyle } from './styles';
import { FileBrowserDialog } from './FileBrowserDialog';

interface Props {
  onLoaded: (stats: StatsResponse) => void;
  onRoadNetworkFetched: (geojson: GeoJSON.FeatureCollection) => void;
}

export function LoadPanel({ onLoaded, onRoadNetworkFetched }: Props) {
  const [path, setPath] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stats, setStats] = useState<StatsResponse | null>(null);
  const [networkVisible, setNetworkVisible] = useState(false);
  const [networkBusy, setNetworkBusy] = useState(false);
  const [browseOpen, setBrowseOpen] = useState(false);

  async function handleLoad() {
    setBusy(true);
    setError(null);
    try {
      const s = await loadRouterDb(path);
      setStats(s);
      onLoaded(s);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function handleToggleNetwork() {
    if (networkVisible) {
      setNetworkVisible(false);
      onRoadNetworkFetched({ type: 'FeatureCollection', features: [] });
      return;
    }
    setNetworkBusy(true);
    try {
      const g = await fetchRoadNetwork();
      onRoadNetworkFetched(g);
      setNetworkVisible(true);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setNetworkBusy(false);
    }
  }

  return (
    <section style={panelStyle}>
      <h2 style={h2Style}>RouterDb 読込</h2>
      <div style={{ display: 'flex', gap: 8 }}>
        <input
          style={{ flex: 1, padding: '6px 8px', fontFamily: 'monospace', fontSize: 13 }}
          placeholder="C:/path/to/default.routerdb"
          value={path}
          onChange={(e) => setPath(e.target.value)}
          disabled={busy}
        />
        <button onClick={() => setBrowseOpen(true)} disabled={busy} style={btnStyle}>ファイル参照…</button>
      </div>
      <div style={{ marginTop: 6 }}>
        <button onClick={handleLoad} disabled={busy || path.trim() === ''} style={btnStyle}>
          {busy ? '読込中…' : '読込'}
        </button>
      </div>
      {browseOpen && (
        <FileBrowserDialog
          title="RouterDb ファイルを選択"
          pattern="*.routerdb"
          rememberKey="mv-routerdb-dir"
          onClose={() => setBrowseOpen(false)}
          onSelect={(p) => { setPath(p); setBrowseOpen(false); }}
        />
      )}
      {error && <p style={errorStyle}>{error}</p>}
      {stats && (
        <>
          <dl style={dlStyle}>
            <dt>頂点数</dt>
            <dd>{stats.vertexCount.toLocaleString()}</dd>
            <dt>辺数</dt>
            <dd>{stats.edgeCount.toLocaleString()}</dd>
            <dt>SW</dt>
            <dd>{stats.southWest.latitude.toFixed(5)}, {stats.southWest.longitude.toFixed(5)}</dd>
            <dt>NE</dt>
            <dd>{stats.northEast.latitude.toFixed(5)}, {stats.northEast.longitude.toFixed(5)}</dd>
          </dl>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>
            <input
              type="checkbox"
              checked={networkVisible}
              disabled={networkBusy}
              onChange={handleToggleNetwork}
            />
            道路ネットワークを表示 {networkBusy ? '（取得中…）' : ''}
          </label>
        </>
      )}
    </section>
  );
}

const dlStyle: React.CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'auto 1fr',
  gap: '2px 8px',
  margin: '8px 0 0',
  fontSize: 13,
};
