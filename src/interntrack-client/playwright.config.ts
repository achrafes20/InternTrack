import { defineConfig, devices } from '@playwright/test';

/**
 * Tests de bout en bout contre la stack Docker complète (nginx + API + PostgreSQL).
 *   docker compose up -d --build   puis   npm run e2e
 * E2E_BASE_URL permet de viser un autre environnement (préproduction…).
 * En CI, l'API est démarrée avec des quotas de connexion relevés (RateLimiting__LoginPerMinute).
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    locale: 'fr-FR',
    timezoneId: 'Africa/Casablanca',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
