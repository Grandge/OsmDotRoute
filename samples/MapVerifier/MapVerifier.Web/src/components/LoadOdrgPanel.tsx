import { useState } from 'react';
import { fetchOdrgRoadNetwork, loadOdrg, type OdrgStatsResponse } from '../api/client';
import { panelStyle, h2Style, btnStyle, errorStyle } from './styles';
import { FileBrowserDialog } from './FileBrowserDialog';

interface Props {
  onLoaded: (stats: OdrgStatsResponse) => void;
  onRoadNetworkFetched: (geojson: GeoJSON.FeatureCollection) => void;
}

/**
 * Phase 2 ステップ 5.4。`.odrg` ファイルを読込んで地図にオーバーレイ表示する。
 * 既存 LoadPanel (RouterDb 読込) と並列に動作し、RouterDb (青) と .odrg (赤) を
 * 重ね表示してズレを目視確認するためのパネル。
 */
export function LoadOdrgPanel({ onLoaded, onRoadNetworkFetched }: Props) {
  const [path, setPath] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stats, setStats] = useState<OdrgStatsResponse | null>(null);
  const [networkVisible, setNetworkVisible] = useState(false);
  const [networkBusy, setNetworkBusy] = useState(false);
  const [browseOpen, setBrowseOpen] = useState(false);

  async function handleLoad() {
    setBusy(true);
    setError(null);
    try {
      const s = await loadOdrg(path);
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
      const g = await fetchOdrgRoadNetwork();
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
      <h2 style={h2Style}>.odrg 読込 (Phase 2 検証)</h2>
      <div style={{ display: 'flex', gap: 8 }}>
        <input
          style={{ flex: 1, padding: '6px 8px', fontFamily: 'monospace', fontSize: 13 }}
          placeholder="C:/path/to/tsushima.odrg"
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
          title=".odrg ファイルを選択"
          pattern="*.odrg"
          rememberKey="mv-odrg-dir"
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
            <dt>profiles</dt>
            <dd>{stats.profileNames.join(', ')}</dd>
          </dl>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>
            <input
              type="checkbox"
              checked={networkVisible}
              disabled={networkBusy}
              onChange={handleToggleNetwork}
            />
            道路ネットワーク（赤・破線）を表示 {networkBusy ? '（取得中…）' : ''}
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
