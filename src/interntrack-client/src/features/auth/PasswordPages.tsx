import React, { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faKey, faCheck, faTriangleExclamation } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { apiErrorMessage } from '../../types';

/** Carte centrée utilisée par les pages publiques (mot de passe, signature entreprise). */
export const PublicCard: React.FC<{ title: string; subtitle?: string; children: React.ReactNode }> = ({ title, subtitle, children }) => (
  <div className="min-h-screen flex items-center justify-center bg-slate-100 p-4">
    <div className="w-full max-w-md bg-white rounded-2xl shadow-lg p-7">
      <div className="text-xs font-bold text-blue-600 tracking-wide mb-1">INTERNTRACK · ENSA</div>
      <h1 className="text-xl font-bold text-gray-900">{title}</h1>
      {subtitle && <p className="text-sm text-gray-500 mt-1">{subtitle}</p>}
      <div className="mt-5">{children}</div>
    </div>
  </div>
);

const inputClass = 'w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30 focus:border-blue-500 box-border';

export const Alert: React.FC<{ kind: 'ok' | 'error'; children: React.ReactNode }> = ({ kind, children }) => (
  <div className={`p-3 rounded-lg text-sm mb-4 ${kind === 'ok' ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-700'}`}>
    <FontAwesomeIcon icon={kind === 'ok' ? faCheck : faTriangleExclamation} className="mr-2" />{children}
  </div>
);

export const ForgotPasswordPage: React.FC = () => {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(''); setLoading(true);
    try {
      await api.post('/auth/forgot-password', { email });
      setSent(true);
    } catch (err: any) {
      setError(apiErrorMessage(err, 'La demande n\'a pas pu être envoyée. Réessayez dans quelques minutes.'));
    } finally { setLoading(false); }
  };

  return (
    <PublicCard title="Mot de passe oublié" subtitle="Recevez par e-mail un lien pour choisir un nouveau mot de passe.">
      {sent ? (
        <Alert kind="ok">
          Si un compte actif correspond à cette adresse, un lien de réinitialisation vient d'être envoyé. Il est valable 2 heures.
        </Alert>
      ) : (
        <form onSubmit={submit} className="flex flex-col gap-3">
          {error && <Alert kind="error">{error}</Alert>}
          <label className="text-sm font-semibold text-gray-700">E-mail institutionnel
            <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} className={`${inputClass} mt-1`} autoComplete="email" />
          </label>
          <button type="submit" disabled={loading || !email}
            className="py-2.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-60 text-white rounded-lg font-semibold text-sm">
            <FontAwesomeIcon icon={faKey} className="mr-2" />{loading ? 'Envoi...' : 'Envoyer le lien'}
          </button>
        </form>
      )}
      <Link to="/login" className="block text-center text-sm text-blue-600 hover:underline mt-5">Retour à la connexion</Link>
    </PublicCard>
  );
};

export const ResetPasswordPage: React.FC = () => {
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const mismatch = confirm.length > 0 && password !== confirm;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirm) return;
    setError(''); setLoading(true);
    try {
      await api.post('/auth/reset-password', { email, token, newPassword: password });
      setDone(true);
    } catch (err: any) {
      setError(apiErrorMessage(err, 'La réinitialisation a échoué.'));
    } finally { setLoading(false); }
  };

  if (!email || !token) {
    return (
      <PublicCard title="Lien incomplet">
        <Alert kind="error">Ce lien de réinitialisation est incomplet. Faites une nouvelle demande.</Alert>
        <Link to="/forgot-password" className="text-sm text-blue-600 hover:underline">Nouvelle demande</Link>
      </PublicCard>
    );
  }

  return (
    <PublicCard title="Nouveau mot de passe" subtitle={email}>
      {done ? (
        <>
          <Alert kind="ok">Votre mot de passe a été modifié. Vos autres sessions ont été fermées.</Alert>
          <Link to="/login" className="block text-center py-2.5 bg-blue-600 text-white rounded-lg font-semibold text-sm">Se connecter</Link>
        </>
      ) : (
        <form onSubmit={submit} className="flex flex-col gap-3">
          {error && <Alert kind="error">{error}</Alert>}
          <label className="text-sm font-semibold text-gray-700">Nouveau mot de passe
            <input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} className={`${inputClass} mt-1`} autoComplete="new-password" />
          </label>
          <label className="text-sm font-semibold text-gray-700">Confirmation
            <input type="password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} className={`${inputClass} mt-1`} autoComplete="new-password" />
          </label>
          {mismatch && <div className="text-xs text-red-600">Les deux mots de passe ne correspondent pas.</div>}
          <div className="text-xs text-gray-500">8 caractères minimum, avec majuscule, minuscule, chiffre et caractère spécial.</div>
          <button type="submit" disabled={loading || !password || mismatch}
            className="py-2.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-60 text-white rounded-lg font-semibold text-sm">
            {loading ? 'Enregistrement...' : 'Enregistrer le mot de passe'}
          </button>
        </form>
      )}
    </PublicCard>
  );
};
