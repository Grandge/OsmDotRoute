import { useEffect, useState } from 'react';

interface Props {
  currentBounds: { sw: [number, number]; ne: [number, number] } | null;
  onApply: (sw: [number, number], ne: [number, number]) => void;
}

export function MapBoundsPanel({ currentBounds, onApply }: Props) {
  const [swLat, setSwLat] = useState('');
  const [swLon, setSwLon] = useState('');
  const [neLat, setNeLat] = useState('');
  const [neLon, setNeLon] = useState('');

  useEffect(() => {
    if (!currentBounds) return;
    setSwLat(currentBounds.sw[0].toFixed(5));
    setSwLon(currentBounds.sw[1].toFixed(5));
    setNeLat(currentBounds.ne[0].toFixed(5));
    setNeLon(currentBounds.ne[1].toFixed(5));
  }, [currentBounds]);

  function handleApply() {
    const sw: [number, number] = [parseFloat(swLat), parseFloat(swLon)];
    const ne: [number, number] = [parseFloat(neLat), parseFloat(neLon)];
    if ([...sw, ...ne].some((n) => Number.isNaN(n))) return;
    onApply(sw, ne);
  }

  return (
    <section style={panelStyle}>
      <h2 style={h2Style}>表示範囲（マップ Bounds）</h2>
      <div style={gridStyle}>
        <label>SW 緯度</label>
        <input value={swLat} onChange={(e) => setSwLat(e.target.value)} />
        <label>SW 経度</label>
        <input value={swLon} onChange={(e) => setSwLon(e.target.value)} />
        <label>NE 緯度</label>
        <input value={neLat} onChange={(e) => setNeLat(e.target.value)} />
        <label>NE 経度</label>
        <input value={neLon} onChange={(e) => setNeLon(e.target.value)} />
      </div>
      <button onClick={handleApply} style={{ marginTop: 8 }} disabled={!currentBounds}>
        この範囲にフィット
      </button>
    </section>
  );
}

const panelStyle: React.CSSProperties = {
  background: '#f9fafb',
  border: '1px solid #d1d5db',
  borderRadius: 6,
  padding: 12,
  marginBottom: 10,
};
const h2Style: React.CSSProperties = { margin: '0 0 8px', fontSize: 14, fontWeight: 600 };
const gridStyle: React.CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'auto 1fr',
  gap: '4px 8px',
  fontSize: 13,
  alignItems: 'center',
};
