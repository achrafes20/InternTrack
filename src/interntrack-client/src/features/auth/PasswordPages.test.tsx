import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { api } from '../../api/client';
import { ForgotPasswordPage, ResetPasswordPage } from './PasswordPages';

const renderAt = (ui: ReactElement, url = '/') => render(<MemoryRouter initialEntries={[url]}>{ui}</MemoryRouter>);

describe('Mot de passe oublié', () => {
  it('envoie la demande puis affiche un message neutre (pas de fuite d\'existence du compte)', async () => {
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} });
    renderAt(<ForgotPasswordPage />);

    await userEvent.type(screen.getByLabelText(/e-mail institutionnel/i), 'etudiant@ensa.ma');
    await userEvent.click(screen.getByRole('button', { name: /envoyer le lien/i }));

    expect(post).toHaveBeenCalledWith('/auth/forgot-password', { email: 'etudiant@ensa.ma' });
    expect(await screen.findByText(/si un compte actif correspond/i)).toBeInTheDocument();
  });

  it('affiche l\'erreur renvoyée par l\'API', async () => {
    vi.spyOn(api, 'post').mockRejectedValue({ response: { status: 429, data: { detail: 'Trop de demandes.' } } });
    renderAt(<ForgotPasswordPage />);

    await userEvent.type(screen.getByLabelText(/e-mail institutionnel/i), 'etudiant@ensa.ma');
    await userEvent.click(screen.getByRole('button', { name: /envoyer le lien/i }));

    expect(await screen.findByRole('button', { name: /envoyer le lien/i })).toBeEnabled();
    expect(screen.queryByText(/si un compte actif correspond/i)).not.toBeInTheDocument();
  });
});

describe('Réinitialisation du mot de passe', () => {
  it('signale un lien incomplet', () => {
    renderAt(<ResetPasswordPage />, '/reset-password?email=a%40ensa.ma');
    expect(screen.getByText(/lien de réinitialisation est incomplet/i)).toBeInTheDocument();
  });

  it('bloque l\'envoi tant que la confirmation ne correspond pas', async () => {
    const post = vi.spyOn(api, 'post');
    renderAt(<ResetPasswordPage />, '/reset-password?email=a%40ensa.ma&token=abc');

    const [password, confirm] = screen.getAllByLabelText(/mot de passe|confirmation/i);
    await userEvent.type(password, 'Secret@2026');
    await userEvent.type(confirm, 'Secret@2027');

    expect(screen.getByText(/ne correspondent pas/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /enregistrer le mot de passe/i })).toBeDisabled();
    expect(post).not.toHaveBeenCalled();
  });

  it('envoie e-mail, jeton et nouveau mot de passe', async () => {
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} });
    renderAt(<ResetPasswordPage />, '/reset-password?email=a%40ensa.ma&token=abc');

    const [password, confirm] = screen.getAllByLabelText(/mot de passe|confirmation/i);
    await userEvent.type(password, 'Secret@2026');
    await userEvent.type(confirm, 'Secret@2026');
    await userEvent.click(screen.getByRole('button', { name: /enregistrer le mot de passe/i }));

    expect(post).toHaveBeenCalledWith('/auth/reset-password', { email: 'a@ensa.ma', token: 'abc', newPassword: 'Secret@2026' });
    expect(await screen.findByText(/votre mot de passe a été modifié/i)).toBeInTheDocument();
  });
});
