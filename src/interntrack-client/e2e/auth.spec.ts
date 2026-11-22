import { expect, test } from '@playwright/test';
import { ACCOUNTS, loginThroughUi } from './helpers';

test.describe('Authentification', () => {
  test('connexion de l\'administrateur puis session conservée après rechargement', async ({ page }) => {
    await loginThroughUi(page, ACCOUNTS.admin.email, ACCOUNTS.admin.password);

    // Le jeton d'accès est en mémoire : au rechargement, la session est restaurée via le cookie HttpOnly.
    await page.reload();
    await expect(page).toHaveURL(/\/dashboard/);

    const cookies = await page.context().cookies();
    const refresh = cookies.find((c) => c.name === 'interntrack_refresh_token');
    expect(refresh, 'cookie de session').toBeDefined();
    expect(refresh!.httpOnly).toBe(true);
  });

  test('mot de passe erroné : message d\'erreur et reste sur la page de connexion', async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[type="email"]').fill(ACCOUNTS.admin.email);
    await page.locator('input[type="password"]').fill('Mauvais@123456');
    await page.locator('button[type="submit"]').click();

    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByText(/incorrect|invalide|échec|erreur/i).first()).toBeVisible();
  });

  test('une page protégée redirige vers la connexion sans session', async ({ page }) => {
    await page.goto('/internships');
    await expect(page).toHaveURL(/\/login/);
  });

  test('mot de passe oublié : réponse neutre', async ({ page }) => {
    await page.goto('/forgot-password');
    await page.getByLabel(/e-mail institutionnel/i).fill('inconnu@ensa.ma');
    await page.getByRole('button', { name: /envoyer le lien/i }).click();
    await expect(page.getByText(/si un compte actif correspond/i)).toBeVisible();
  });
});

test.describe('Liens publics', () => {
  test('lien de signature entreprise invalide', async ({ page }) => {
    await page.goto('/convention/sign/jeton-invalide-e2e');
    await expect(page.getByText(/invalide|expiré|déjà été utilisé/i).first()).toBeVisible();
  });

  test('lien d\'évaluation tuteur invalide', async ({ page }) => {
    await page.goto('/defense/evaluate/jeton-invalide-e2e');
    await expect(page.getByText(/invalide|expiré/i).first()).toBeVisible();
  });
});
