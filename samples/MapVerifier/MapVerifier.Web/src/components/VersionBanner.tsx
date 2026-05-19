import { useEffect, useState } from 'react';
import { WEB_VERSION } from '../version';
import { fetchVersion } from '../api/client';

export function VersionBanner() {
  const [serverVersion, setServerVersion] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchVersion()
      .then((v) => setServerVersion(v.version))
      .catch((e: Error) => setError(e.message));
  }, []);

  const mismatch = serverVersion !== null && serverVersion !== WEB_VERSION;
  const serverLabel = error ? `server: ⚠ ${error}` : `server: v${serverVersion ?? '…'}`;

  return (
    <div
      style={{
        padding: '6px 12px',
        background: mismatch ? '#ffe9b0' : '#1f2937',
        color: mismatch ? '#5a3b00' : '#e5e7eb',
        fontFamily: 'system-ui, sans-serif',
        fontSize: 13,
        display: 'flex',
        gap: 12,
        alignItems: 'center',
      }}
    >
      <strong style={{ fontSize: 14 }}>MapVerifier v{WEB_VERSION}</strong>
      <span style={{ opacity: 0.85 }}>{serverLabel}</span>
      {mismatch && <span style={{ marginLeft: 'auto' }}>⚠ バージョン不一致</span>}
    </div>
  );
}
