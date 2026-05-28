import type { CSSProperties } from 'react';

export const panelStyle: CSSProperties = {
  background: '#f9fafb',
  border: '1px solid #d1d5db',
  borderRadius: 6,
  padding: 12,
  marginBottom: 10,
};

export const BUILTIN_DIFFICULTIES = [
  'flooding',
  'liquefaction',
  'landslide',
  'construction',
  'obstacle',
  'congestion',
  'snow',
  'ice',
] as const;
