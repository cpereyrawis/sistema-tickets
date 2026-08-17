import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    // Proxy al backend: el navegador ve un único origen, así que no hace falta CORS ni
    // configurar orígenes permitidos. En producción el frontend compilado se sirve desde
    // la misma aplicación ASP.NET Core, con lo cual la situación es la misma (§11.2).
    proxy: {
      '/api': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
    },
  },
});
