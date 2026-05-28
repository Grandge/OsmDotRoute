import { useEffect, useState } from 'react';
import { listRestrictions, deleteRestriction, clearAllRestrictions, type RestrictionItem } from '../api/client';
import { panelStyle, h2Style, btnStyle, errorStyle } from './styles';

interface Props {
  refreshNonce: number;
  onChanged: () => void;
}

export function RestrictionListPanel({ refreshNonce, onChanged }: Props) {
  const [items, setItems] = useState<RestrictionItem[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setBusy(true);
    setError(null);
    try {
      const res = await listRestrictions();
      setItems(res.items);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void load();
  }, [refreshNonce]);

  async function handleDelete(id: string) {
    try {
      await deleteRestriction(id);
      onChanged();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  async function handleClearAll() {
    if (items.length === 0) return;
    if (!window.confirm(`全制約 ${items.length} 件を削除しますか?`)) return;
    try {
      await clearAllRestrictions();
      onChanged();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  return (
    <section style={panelStyle}>
      <h2 style={h2Style}>登録済み制約 ({items.length})</h2>
      <div style={{ display: 'flex', gap: 8, marginBottom: 6 }}>
        <button onClick={load} disabled={busy} style={btnStyle}>再読込</button>
        <button onClick={handleClearAll} disabled={items.length === 0} style={btnStyle}>全削除</button>
      </div>
      {error && <p style={errorStyle}>{error}</p>}
      {items.length === 0 && <p style={{ fontSize: 12, color: '#6b7280', margin: 0 }}>制約は登録されていません</p>}
      {items.length > 0 && (
        <div style={{ maxHeight: 260, overflowY: 'auto', border: '1px solid #e5e7eb', borderRadius: 4 }}>
          <table style={{ width: '100%', fontSize: 12, borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ background: '#f3f4f6' }}>
                <th style={th}>種別</th>
                <th style={th}>難所</th>
                <th style={th}>形状</th>
                <th style={th}>タグ</th>
                <th style={th}></th>
              </tr>
            </thead>
            <tbody>
              {items.map((it) => (
                <tr key={it.id} style={{ borderTop: '1px solid #e5e7eb' }}>
                  <td style={td}>{it.kind}</td>
                  <td style={td}>{it.difficultyType ?? '—'}</td>
                  <td style={td}>
                    {it.shapeType === 'mesh' ? `mesh×${it.meshCodes?.length ?? 0}` : `polygon×${it.outerBoundary?.length ?? 0}`}
                  </td>
                  <td style={td}>{it.tag ?? '—'}</td>
                  <td style={td}>
                    <button onClick={() => handleDelete(it.id)} style={{ ...btnStyle, padding: '2px 6px' }}>削除</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

const th: React.CSSProperties = { padding: '4px 6px', textAlign: 'left', fontWeight: 600 };
const td: React.CSSProperties = { padding: '4px 6px' };
