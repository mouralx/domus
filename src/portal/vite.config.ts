import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({
  plugins: [react()],
  server: {
    port: 4173,
    strictPort: true,
    allowedHosts: ['kanbada.mouras.me'],
    proxy: {
      '/automations-api': {
        target: process.env.AUTOMATIONS_PROXY_TARGET || 'http://127.0.0.1:4310',
        changeOrigin: false,
        rewrite: (path) => path.replace(/^\/automations-api/, ''),
      },
      '/api': {
        target: process.env.API_PROXY_TARGET || 'http://127.0.0.1:5180',
        changeOrigin: false,
      },
    },
  },
});
