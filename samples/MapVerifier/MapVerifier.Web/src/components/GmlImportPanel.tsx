import { useState } from 'react';
import { importGmlFile, type Kind } from '../api/client';
import { panelStyle, h2Style, btnStyle, inputStyle, errorStyle, BUILTIN_DIFFICULTIES } from './styles';
import { FileBrowserDialog } from './FileBrowserDialog';

interface Props {
  currentBounds: { sw: [number, number]; ne: [number, number] } | null;
  onImported: () => void;
}

export function GmlImportPanel({ currentBounds, onImported }: Props) {
  const [filePath, setFilePath] = useState('');
  const [kind, setKind] = useState<Kind>('difficulty');
  const [difficulty, setDifficulty] = useState<string>('flooding');
  const [useBounds, setUseBounds] = useState(true);
  const [tag, setTag] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<string | null>(null);
  const [browseOpen, setBrowseOpen] = useState(false);

  async function handleImport() {
    if (filePath.trim() === '') return;
    if (useBounds && !currentBounds) {
      setError('現在のマップ範囲が未取得です（RouterDb を先に読み込んでください）。');
      return;
    }
    setBusy(true);
    setError(null);
    setLastResult(null);
    try {
      const t0 = performance.now();
      const res = await importGmlFile({
        filePath,
        kind,
        difficultyType: kind === 'difficulty' ? difficulty : undefined,
        useMapBounds: useBounds,
        mapBoundsSouthWest: useBounds && currentBounds
          ? { latitude: currentBounds.sw[0], longitude: currentBounds.sw[1] }
          : undefined,
        mapBoundsNorthEast: useBounds && currentBounds
          ? { latitude: currentBounds.ne[0], longitude: currentBounds.ne[1] }
          : undefined,
        tag: tag.trim() === '' ? undefined : tag.trim(),
      });
      const elapsed = ((performance.now() - t0) / 1000).toFixed(1);
      setLastResult(`${res.acceptedCount} 件をインポートしました (${elapsed} 秒)`);
      onImported();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section style={panelStyle}>
      <h2 style={h2Style}>GML ファイル インポート</h2>
      <div style={{ display: 'flex', gap: 8, marginBottom: 6 }}>
        <input
          style={{ flex: 1, padding: '6px 8px', fontFamily: 'monospace', fontSize: 12 }}
          placeholder="C:/path/to/A31-12_24.xml"
          value={filePath}
          onChange={(e) => setFilePath(e.target.value)}
          disabled={busy}
        />
        <button onClick={() => setBrowseOpen(true)} disabled={busy} style={btnStyle}>ファイル参照…</button>
      </div>
      {browseOpen && (
        <FileBrowserDialog
          title="GML ファイルを選択"
          pattern="*.xml;*.gml"
          rememberKey="mv-gml-dir"
          onClose={() => setBrowseOpen(false)}
          onSelect={(p) => { setFilePath(p); setBrowseOpen(false); }}
        />
      )}
      <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '4px 8px', fontSize: 13, alignItems: 'center' }}>
        <label>種別</label>
        <select value={kind} onChange={(e) => setKind(e.target.value as Kind)} style={inputStyle}>
          <option value="block">block (進入不可)</option>
          <option value="difficulty">difficulty (難所)</option>
        </select>
        {kind === 'difficulty' && (
          <>
            <label>難所タイプ</label>
            <select value={difficulty} onChange={(e) => setDifficulty(e.target.value)} style={inputStyle}>
              {BUILTIN_DIFFICULTIES.map((d) => (
                <option key={d} value={d}>{d}</option>
              ))}
            </select>
          </>
        )}
        <label>タグ (任意)</label>
        <input value={tag} onChange={(e) => setTag(e.target.value)} style={inputStyle} />
      </div>
      <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 6, fontSize: 13 }}>
        <input
          type="checkbox"
          checked={useBounds}
          onChange={(e) => setUseBounds(e.target.checked)}
        />
        現在のマップ範囲でフィルタする (REQ-RST-040)
      </label>
      <div style={{ marginTop: 8 }}>
        <button onClick={handleImport} disabled={busy || filePath.trim() === ''} style={btnStyle}>
          {busy ? 'インポート中…' : 'インポート'}
        </button>
      </div>
      {lastResult && <p style={{ fontSize: 13, margin: '6px 0 0', color: '#15803d' }}>{lastResult}</p>}
      {error && <p style={errorStyle}>{error}</p>}
    </section>
  );
}
