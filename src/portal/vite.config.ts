import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({
  plugins: [react()],
  server: {
    port: 4173,
    strictPort: true,
    allowedHosts: ['domus.mouras.me'],
    proxy: {
      '/auth/callback': {
        target: process.env.AI_PROXY_TARGET || 'http://127.0.0.1:4330',
        changeOrigin: false,
        rewrite: (path) => path.replace(/^\/auth\/callback/, '/oauth/callback'),
      },
      '/ai-api': {
        target: process.env.AI_PROXY_TARGET || 'http://127.0.0.1:4330',
        changeOrigin: false,
        rewrite: (path) => path.replace(/^\/ai-api/, ''),
      },
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
