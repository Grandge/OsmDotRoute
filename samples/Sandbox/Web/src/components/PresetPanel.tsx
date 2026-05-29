import { useState } from 'react';
import { loadOdrg, type StatsResponse } from '../api/client';
import { panelStyle, h2Style, btnStyle, inputStyle, errorStyle } from './styles';
import { useI18n } from '../i18n';

// WASM デモ専用。事前ビルド .odrg（data/ 同梱）を選んでブラウザ内ロードする（Phase 3 ステップ 3J.5、J-3）。
const PRESETS = [{ file: 'tsushima.odrg', labelKey: 'pp.presetTsushima' }];

export function PresetPanel({ onLoaded }: { onLoaded: (stats: StatsResponse) => void }) {
  const { t } = useI18n();
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
      <h3 style={h2Style}>{t('pp.title')}</h3>
      <select
        value={file}
        onChange={(e) => setFile(e.target.value)}
        disabled={loading}
        style={{ ...inputStyle, width: '100%', marginBottom: 8 }}
      >
        {PRESETS.map((p) => (
          <option key={p.file} value={p.file}>{t(p.labelKey)}</option>
        ))}
      </select>
      <button onClick={handleLoad} disabled={loading} style={{ ...btnStyle, width: '100%' }}>
        {loading ? t('pp.loading') : t('common.load')}
      </button>
      <div style={{ fontSize: 11, color: '#6b7280', marginTop: 6 }}>
        {t('pp.desc')}
      </div>
      {error && <p style={errorStyle}>{error}</p>}
    </div>
  );
}
