import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faShieldHalved, faClipboardList, faChalkboardUser,
  faUserGraduate, faArrowRightToBracket, faSpinner, faTriangleExclamation
} from '@fortawesome/free-solid-svg-icons';

interface RoleCard {
  role:        string;
  email:       string;
  password:    string;
  icon:        any;
  colorClass:  string;
  borderClass: string;
  bgClass:     string;
}

const ROLE_CARDS: RoleCard[] = [
  {
    role: 'Administrateur',
    email: 'admin@ensa.ma', password: 'Admin@123456',
    icon: faShieldHalved,
    colorClass: 'text-red-600', borderClass: 'border-red-200', bgClass: 'bg-red-50 hover:bg-red-100',
  },
  {
    role: 'Responsable GI',
    email: 'responsable.gi@ensa.ma', password: 'Resp@123456',
    icon: faClipboardList,
    colorClass: 'text-amber-600', borderClass: 'border-amber-200', bgClass: 'bg-amber-50 hover:bg-amber-100',
  },
  {
    role: 'Encadrant',
    email: 'prof.tazi@ensa.ma', password: 'Prof@123456',
    icon: faChalkboardUser,
    colorClass: 'text-violet-600', borderClass: 'border-violet-200', bgClass: 'bg-violet-50 hover:bg-violet-100',
  },
  {
    role: 'Étudiant (Achraf)',
    email: 'etudiant.achraf@ensa.ma', password: 'Student@123456',
    icon: faUserGraduate,
    colorClass: 'text-blue-600', borderClass: 'border-blue-200', bgClass: 'bg-blue-50 hover:bg-blue-100',
  },
  {
    role: 'Étudiante (Fatima)',
    email: 'etudiant.fatima@ensa.ma', password: 'Student@123456',
    icon: faUserGraduate,
    colorClass: 'text-sky-600', borderClass: 'border-sky-200', bgClass: 'bg-sky-50 hover:bg-sky-100',
  },
];

export const LoginPage: React.FC = () => {
  const [email,    setEmail]    = useState('');
  const [password, setPassword] = useState('');
  const [error,    setError]    = useState<string | null>(null);
  const [loading,  setLoading]  = useState(false);
  const [active,   setActive]   = useState<string | null>(null);

  const { login }  = useAuth();
  const navigate   = useNavigate();

  const doLogin = async (em: string, pw: string) => {
    setError(null);
    setLoading(true);
    try {
      await login(em, pw);
      navigate('/dashboard');
    } catch (err: any) {
      setError(err.response?.data?.detail || 'Identifiants invalides ou compte inactif.');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    doLogin(email, password);
  };

  const handleCard = (card: RoleCard) => {
    setActive(card.role);
    setEmail(card.email);
    setPassword(card.password);
    doLogin(card.email, card.password);
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-ensa-navy via-ensa-blue to-sky-400 flex items-center justify-center p-6">
      <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl overflow-hidden">

        {/* En-tête */}
        <div className="bg-gradient-to-r from-ensa-navy to-ensa-blue px-8 py-8 text-center text-white">
          <div className="w-16 h-16 bg-white rounded-2xl mx-auto mb-4 flex items-center justify-center shadow-lg">
            <span className="text-2xl font-black text-blue-600 tracking-tighter">SF</span>
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight">StageFlow</h1>
          <p className="text-sm opacity-80 mt-1">ENSA &middot; Portail de Gestion des Stages & PFE</p>
        </div>

        <div className="px-8 py-7">

          {/* Erreur */}
          {error && (
            <div className="flex items-center gap-2 bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 mb-5 text-sm">
              <FontAwesomeIcon icon={faTriangleExclamation} />
              <span>{error}</span>
            </div>
          )}

          {/* Formulaire */}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                Adresse e-mail institutionnelle
              </label>
              <input
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="nom@ensa.ma"
                required
                className="w-full px-4 py-2.5 rounded-lg border border-slate-300 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 transition"
              />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                Mot de passe
              </label>
              <input
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                placeholder="••••••••"
                required
                className="w-full px-4 py-2.5 rounded-lg border border-slate-300 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 transition"
              />
            </div>
            <button
              type="submit"
              disabled={loading}
              className="w-full flex items-center justify-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-bold py-3 rounded-xl text-sm transition mt-2"
            >
              {loading
                ? <><FontAwesomeIcon icon={faSpinner} spin /> Connexion en cours...</>
                : <><FontAwesomeIcon icon={faArrowRightToBracket} /> Se connecter</>
              }
            </button>
          </form>

          {/* Connexion rapide */}
          <div className="mt-6 pt-5 border-t border-slate-100">
            <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider text-center mb-3">
              Connexion rapide — Comptes de démonstration
            </p>
            <div className="grid grid-cols-2 gap-2">
              {ROLE_CARDS.map(card => (
                <button
                  key={card.role}
                  onClick={() => handleCard(card)}
                  disabled={loading}
                  className={`flex flex-col items-start gap-1 p-3 rounded-xl border-2 text-left transition cursor-pointer
                    ${active === card.role ? card.borderClass + ' ' + card.bgClass : 'border-slate-200 bg-slate-50 hover:border-slate-300'}
                    ${loading ? 'opacity-50 cursor-not-allowed' : ''}`}
                >
                  <div className={`flex items-center gap-2 font-semibold text-sm ${card.colorClass}`}>
                    <FontAwesomeIcon icon={card.icon} className="text-xs" />
                    {card.role}
                  </div>
                  <div className="text-xs text-slate-400 truncate w-full">{card.email}</div>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
export default LoginPage;
