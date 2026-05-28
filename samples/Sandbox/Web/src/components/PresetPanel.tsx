import { useState } from 'react';
import { loadOdrg, type StatsResponse } from '../api/client';
import { panelStyle, h2Style, btnStyle, inputStyle, errorStyle } from './styles';

// WASM デモ専用。事前ビルド .odrg（data/ 同梱）を選んでブラウザ内ロードする（Phase 3 ステップ 3J.5、J-3）。
const PRESETS = [{ file: 'tsushima.odrg', label: '津島市 (Tsushima, 愛知県)' }];

export function PresetPanel({ onLoaded }: { onLoaded: (stats: StatsResponse) => void }) {
  const [file, setFile] = useState(PRESETS[0].file);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleLoad() {
    setLoading(true);
    setError(null);
    try {
      const stats = await loadOdrg(file);
      onLoaded(stats);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={panelStyle}>
      <h3 style={h2Style}>事前ビルド .odrg を読み込む</h3>
      <select
        value={file}
        onChange={(e) => setFile(e.target.value)}
        disabled={loading}
        style={{ ...inputStyle, width: '100%', marginBottom: 8 }}
      >
        {PRESETS.map((p) => (
          <option key={p.file} value={p.file}>{p.label}</option>
        ))}
      </select>
      <button onClick={handleLoad} disabled={loading} style={{ ...btnStyle, width: '100%' }}>
        {loading ? '読み込み中…' : 'Load'}
      </button>
      <div style={{ fontSize: 11, color: '#6b7280', marginTop: 6 }}>
        ブラウザ内で経路計算・制約・Re-Route を実行します（サーバー不要）。
      </div>
      {error && <p style={errorStyle}>{error}</p>}
    </div>
  );
}
