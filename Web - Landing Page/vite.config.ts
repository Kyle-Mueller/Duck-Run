import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vitejs.dev/config/
// Static build → dist/, served as-is by Cloudflare Pages.
export default defineConfig({
    plugins: [react()],
    server: {
        port: 54129,
    },
    build: {
        outDir: 'dist',
        sourcemap: false,
    },
});
