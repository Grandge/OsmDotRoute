import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Node 17+ では localhost が IPv6 (::1) に先解決されることがあり、
      // Kestrel の listen タイミングと噛み合わず ECONNREFUSED になる。IPv4 直指定で回避。
      '/api': 'http://127.0.0.1:5279',
    },
  },
});
