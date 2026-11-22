/// <reference types="vitest/config" />
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
  // Tests unitaires et de composants (Vitest + Testing Library). Les tests de bout en bout
  // (Playwright, dossier e2e/) tournent séparément contre la stack Docker.
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      include: ['src/api/**', 'src/features/auth/**'],
      reporter: ['text-summary', 'lcov'],
    },
  },
});
