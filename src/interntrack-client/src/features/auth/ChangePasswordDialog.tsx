import React, { useState } from 'react';
import { Bar, Button, Dialog } from '@ui5/webcomponents-react';
import { api } from '../../api/client';
import { apiErrorMessage } from '../../types';
import { Alert } from './PasswordPages';

const inputClass = 'w-full px-3 py-2 border border-gray-300 rounded-lg text-sm mt-1 box-border';

/** Changement de mot de passe par l'utilisateur connecté (ses autres sessions sont fermées par le serveur). */
export const ChangePasswordDialog: React.FC<{ onClose: () => void }> = ({ onClose }) => {
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);
  const [loading, setLoading] = useState(false);

  const mismatch = confirm.length > 0 && next !== confirm;

  const submit = async () => {
    setError(''); setLoading(true);
    try {
      await api.post('/auth/change-password', { currentPassword: current, newPassword: next });
      setDone(true);
    } catch (err: any) {
      setError(apiErrorMessage(err, 'Le changement de mot de passe a échoué.'));
    } finally { setLoading(false); }
  };

  return (
    <Dialog open headerText="Changer mon mot de passe"
      footer={<Bar endContent={<>
        <Button design="Transparent" onClick={onClose}>{done ? 'Fermer' : 'Annuler'}</Button>
        {!done && (
          <Button design="Emphasized" disabled={!current || !next || mismatch || loading} onClick={submit}>
            {loading ? 'Enregistrement...' : 'Enregistrer'}
          </Button>
        )}
      </>} />}
    >
      <div className="p-4 flex flex-col gap-3" style={{ minWidth: '360px' }}>
        {error && <Alert kind="error">{error}</Alert>}
        {done ? (
          <Alert kind="ok">Mot de passe modifié. Vos sessions sur les autres appareils ont été fermées.</Alert>
        ) : (
          <>
            <label className="text-sm font-semibold text-gray-700">Mot de passe actuel
              <input type="password" value={current} onChange={(e) => setCurrent(e.target.value)} className={inputClass} autoComplete="current-password" />
            </label>
            <label className="text-sm font-semibold text-gray-700">Nouveau mot de passe
              <input type="password" value={next} onChange={(e) => setNext(e.target.value)} className={inputClass} autoComplete="new-password" />
            </label>
            <label className="text-sm font-semibold text-gray-700">Confirmation
              <input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} className={inputClass} autoComplete="new-password" />
            </label>
            {mismatch && <div className="text-xs text-red-600">Les deux mots de passe ne correspondent pas.</div>}
          </>
        )}
      </div>
    </Dialog>
  );
};
