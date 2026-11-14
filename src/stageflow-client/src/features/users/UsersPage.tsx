import React, { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Button, Input, Text, BusyIndicator, Bar, Title, Dialog, Tag, Avatar
} from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faUsers, faShieldHalved, faChalkboardUser, faUserGraduate,
  faUserPlus, faSearch, faTriangleExclamation, faCircle, faGraduationCap,
  faRotateLeft
} from '@fortawesome/free-solid-svg-icons';
import { apiClient } from '../../api/client';
import { UserDto } from '../../types';

const ROLE_CONFIG: Record<string, { label: string; color: string; bg: string; tagScheme: string; avatar: string; icon: any }> = {
  Admin: { label: 'Administrateur', color: '#dc2626', bg: '#fef2f2', tagScheme: '2', avatar: 'Accent2', icon: faShieldHalved },
  ResponsableFiliere: { label: 'Responsable', color: '#d97706', bg: '#fffbeb', tagScheme: '6', avatar: 'Accent6', icon: faChalkboardUser },
  Encadrant: { label: 'Encadrant', color: '#7c3aed', bg: '#f5f3ff', tagScheme: '1', avatar: 'Accent1', icon: faGraduationCap },
  Etudiant: { label: 'Étudiant', color: '#2563eb', bg: '#eff6ff', tagScheme: '5', avatar: 'Accent5', icon: faUserGraduate },
};

export const UsersPage: React.FC = () => {
  const queryClient = useQueryClient();
  const [openModal, setOpenModal] = useState(false);
  const [email, setEmail] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [role, setRole] = useState('Etudiant');
  const [password, setPassword] = useState('Password123!');
  const [errorMessage, setErrorMessage] = useState('');
  const [searchText, setSearchText] = useState('');

  const { data: users = [], isLoading } = useQuery<UserDto[]>({
    queryKey: ['users'],
    queryFn: async () => { const res = await apiClient.get('/users'); return res.data; }
  });

  const createUserMutation = useMutation({
    mutationFn: async () => {
      setErrorMessage('');
      await apiClient.post('/users', { email, firstName, lastName, role, password });
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['users'] }); setOpenModal(false); resetForm(); },
    onError: (err: any) => { setErrorMessage(err.response?.data?.detail || "Erreur lors de la création de l'utilisateur."); }
  });

  const resetForm = () => {
    setEmail(''); setFirstName(''); setLastName('');
    setRole('Etudiant'); setPassword('Password123!'); setErrorMessage('');
  };

  const filtered = useMemo(() =>
    users.filter(u =>
      !searchText ||
      `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(searchText.toLowerCase())
    ), [users, searchText]);

  const counts = useMemo(() => {
    const c: Record<string, number> = { Admin: 0, ResponsableFiliere: 0, Encadrant: 0, Etudiant: 0 };
    users.forEach(u => { if (c[u.role] !== undefined) c[u.role]++; });
    return c;
  }, [users]);

  const getInitials = (u: UserDto) => `${(u.firstName || '')[0] || ''}${(u.lastName || '')[0] || ''}`.toUpperCase();

  const selectStyle = {
    width: '100%', padding: '0.5rem', borderRadius: '6px',
    border: '1px solid #d1d5db', fontSize: '14px', boxSizing: 'border-box' as const
  };

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="bg-gradient-to-r from-ensa-navy to-blue-700 rounded-2xl px-7 py-6 text-white flex justify-between items-center flex-wrap gap-4">
        <div>
          <div className="flex items-center gap-3 text-2xl font-extrabold mb-1">
            <FontAwesomeIcon icon={faUsers} />
            Gestion des Utilisateurs
          </div>
          <div className="opacity-85 text-sm">
            Administration des comptes ENSA, assignation des rôles et contrôle d'accès.
          </div>
        </div>
        <button
          onClick={() => { resetForm(); setOpenModal(true); }}
          className="flex items-center gap-2 px-4 py-2 bg-white text-blue-700 hover:bg-blue-50 rounded-lg font-semibold text-sm transition-colors"
        >
          <FontAwesomeIcon icon={faUserPlus} /> Nouvel utilisateur
        </button>
      </div>

      {/* Stats */}
      <div className="flex gap-3 flex-wrap">
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-2.5 text-sm font-semibold text-gray-700">
          Total : <span className="text-blue-600">{users.length}</span>
        </div>
        {Object.entries(ROLE_CONFIG).map(([r, cfg]) => (
          <div key={r} style={{ background: cfg.bg, border: `1px solid ${cfg.color}30`, color: cfg.color }}
            className="rounded-lg px-4 py-2.5 text-sm font-semibold flex items-center gap-2">
            <FontAwesomeIcon icon={cfg.icon} />
            {cfg.label}s : {counts[r] || 0}
          </div>
        ))}
      </div>

      {/* Search */}
      <div className="bg-white rounded-2xl p-4 border border-gray-200/80 shadow-sm flex items-center gap-3">
        <div className="relative flex-1">
          <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-xs">
            <FontAwesomeIcon icon={faSearch} />
          </div>
          <input
            type="text"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="Rechercher par nom, prénom ou e-mail institutionnel..."
            className="w-full pl-9 pr-4 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
          />
        </div>
        {searchText && (
          <button
            onClick={() => setSearchText('')}
            className="flex items-center gap-1.5 px-3.5 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl text-sm font-semibold transition-all shadow-sm active:scale-95 whitespace-nowrap h-[42px]"
          >
            <FontAwesomeIcon icon={faRotateLeft} />
            Effacer
          </button>
        )}
      </div>

      {/* User Cards */}
      {isLoading ? (
        <div className="flex justify-center py-16"><BusyIndicator active /></div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16 bg-gray-50 rounded-2xl border-2 border-dashed border-gray-300">
          <FontAwesomeIcon icon={faUsers} className="text-5xl text-gray-300 mb-3" />
          <Title level="H4">Aucun utilisateur trouvé</Title>
        </div>
      ) : (
        <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
          {filtered.map(u => {
            const cfg = ROLE_CONFIG[u.role] || ROLE_CONFIG.Etudiant;
            return (
              <div key={u.id} style={{ border: `1px solid ${cfg.color}20` }}
                className="bg-white rounded-xl shadow-sm overflow-hidden hover:shadow-card transition-shadow">
                {/* Top color band */}
                <div style={{ height: '5px', background: cfg.color }} />
                <div className="p-4">
                  <div className="flex items-center gap-3 mb-3">
                    <Avatar initials={getInitials(u)} colorScheme={cfg.avatar as any} style={{ flexShrink: 0 }} />
                    <div className="min-w-0">
                      <div className="font-bold text-sm text-gray-900 truncate">{u.firstName} {u.lastName}</div>
                      <div className="text-xs text-gray-500 truncate">{u.email}</div>
                    </div>
                  </div>
                  <div className="flex justify-between items-center">
                    <span style={{ background: cfg.bg, color: cfg.color }}
                      className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold">
                      <FontAwesomeIcon icon={cfg.icon} />
                      {cfg.label}
                    </span>
                    <span style={{ background: u.isActive ? '#d1fae5' : '#fee2e2', color: u.isActive ? '#065f46' : '#991b1b' }}
                      className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold">
                      <FontAwesomeIcon icon={faCircle} style={{ fontSize: '6px' }} />
                      {u.isActive ? 'Actif' : 'Inactif'}
                    </span>
                  </div>
                  {u.academicTrackCode && (
                    <div className="mt-2 text-xs text-gray-500 flex items-center gap-1">
                      <FontAwesomeIcon icon={faGraduationCap} />
                      Filière : <strong>{u.academicTrackCode}</strong>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create user dialog */}
      <Dialog open={openModal} headerText="Créer un nouvel utilisateur"
        footer={
          <Bar endContent={
            <>
              <Button design="Transparent" onClick={() => setOpenModal(false)}>Annuler</Button>
              <Button design="Emphasized"
                disabled={!email || !firstName || !lastName || !password || createUserMutation.isPending}
                onClick={() => createUserMutation.mutate()}
              >
                {createUserMutation.isPending ? 'Création...' : 'Créer le compte'}
              </Button>
            </>
          } />
        }
      >
        <div className="flex flex-col gap-4 p-4" style={{ minWidth: '400px' }}>
          {errorMessage && (
            <div className="p-2.5 bg-red-50 text-red-700 rounded-lg text-sm">
              <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {errorMessage}
            </div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block mb-1 font-semibold text-xs">Prénom :</label>
              <Input value={firstName} onInput={(e: any) => setFirstName(e.target.value)}
                placeholder="Ex: Yasmine" style={{ width: '100%' }} />
            </div>
            <div>
              <label className="block mb-1 font-semibold text-xs">Nom :</label>
              <Input value={lastName} onInput={(e: any) => setLastName(e.target.value)}
                placeholder="Ex: Bennani" style={{ width: '100%' }} />
            </div>
          </div>
          <div>
            <label className="block mb-1 font-semibold text-xs">E-mail institutionnel :</label>
            <Input value={email} onInput={(e: any) => setEmail(e.target.value)}
              placeholder="Ex: y.bennani@ensa.ac.ma" style={{ width: '100%' }} />
          </div>
          <div>
            <label className="block mb-1 font-semibold text-xs">Rôle :</label>
            <select style={selectStyle} value={role} onChange={(e) => setRole(e.target.value)}>
              <option value="Etudiant">Étudiant — accès à ses stages &amp; rapports</option>
              <option value="Encadrant">Encadrant — suivi pédagogique des étudiants</option>
              <option value="ResponsableFiliere">Responsable de Filière — validation &amp; coordination</option>
              <option value="Admin">Administrateur — accès complet</option>
            </select>
          </div>
          <div>
            <label className="block mb-1 font-semibold text-xs">Mot de passe initial :</label>
            <Input type="Password" value={password} onInput={(e: any) => setPassword(e.target.value)} style={{ width: '100%' }} />
            <div className="text-xs text-gray-400 mt-1">Min. 8 caractères, 1 majuscule, 1 chiffre</div>
          </div>
        </div>
      </Dialog>
    </div>
  );
};
export default UsersPage;
