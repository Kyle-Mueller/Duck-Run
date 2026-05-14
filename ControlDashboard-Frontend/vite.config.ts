import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

const backend = process.env.VITE_BACKEND_URL ?? 'http://localhost:8090';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: false,
    proxy: {
      '/api': { target: backend, changeOrigin: false },
      '/hubs': { target: backend, changeOrigin: false, ws: true },
      '/health': { target: backend, changeOrigin: false },
    },
  },
  build: {
    outDir: path.resolve(__dirname, '../Control Dashboard - Backend/wwwroot'),
    emptyOutDir: true,
    sourcemap: false,
  },
});
