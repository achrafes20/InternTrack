import { defineConfig } from 'vite';

// Configuration du serveur de développement.
// Les requêtes /api sont proxifiées vers l'API ASP.NET Core afin de conserver
// une origine unique côté navigateur (cookies HttpOnly, pas de CORS en dev).
export default defineConfig({
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5009',
        changeOrigin: true,
      },
    },
  },
});
