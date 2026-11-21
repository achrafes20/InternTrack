import React, { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Button, Input, Text, BusyIndicator, Bar, Title, Dialog, Tag, Avatar
} from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faUsers, faShieldHalved, faChalkboardUser, faUserGraduate,
  faUserPlus, faSearch, faTriangleExclamation, faCircle, faGraduationCap,
  faRotateLeft, faUserPen, faLock, faLockOpen, faKey, faUserSlash, faUserCheck
} from '@fortawesome/free-solid-svg-icons';
import { apiClient } from '../../api/client';
import { UserDto, Track, apiErrorMessage } from '../../types';

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
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [searchText, setSearchText] = useState('');
  const [trackId, setTrackId] = useState('');
  const [studentNumber, setStudentNumber] = useState('');
  const [graduationYear, setGraduationYear] = useState('');
  const [newGraduationYear, setNewGraduationYear] = useState('');
  const [roleTarget, setRoleTarget] = useState<UserDto | null>(null);
  const [newRole, setNewRole] = useState('Etudiant');
  const [newTrackId, setNewTrackId] = useState('');
  const [roleError, setRoleError] = useState('');

  const needsTrack = (r: string) => r === 'Etudiant' || r === 'ResponsableFiliere';

  const { data: tracks = [] } = useQuery<Track[]>({
    queryKey: ['tracks'],
    queryFn: async () => (await apiClient.get('/tracks')).data
  });

  const trackCode = (id?: string) => tracks.find(t => t.id === id)?.code;

  const { data: users = [], isLoading } = useQuery<UserDto[]>({
    queryKey: ['users'],
    queryFn: async () => { const res = await apiClient.get('/users'); return res.data; }
  });

  const createUserMutation = useMutation({
    mutationFn: async () => {
      setErrorMessage('');
      await apiClient.post('/users', {
        email, firstName, lastName, role, password,
        academicTrackId: trackId || null,
        studentNumber: role === 'Etudiant' ? studentNumber : null,
        graduationYear: role === 'Etudiant' && graduationYear ? Number(graduationYear) : null
      });
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['users'] }); setOpenModal(false); resetForm(); },
    onError: (err: any) => { setErrorMessage(apiErrorMessage(err, "Erreur lors de la création de l'utilisateur.")); }
  });

  const [notice, setNotice] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);

  const activeMutation = useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) => { await apiClient.put(`/users/${id}/active`, { isActive }); },
    onSuccess: (_, v) => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setNotice({ kind: 'ok', text: v.isActive ? 'Compte réactivé.' : 'Compte désactivé : ses sessions ont été fermées.' });
    },
    onError: (err: any) => setNotice({ kind: 'error', text: apiErrorMessage(err, "Impossible de modifier l'état du compte.") }),
  });

  const resetLinkMutation = useMutation({
    mutationFn: async (id: string) => { await apiClient.post(`/users/${id}/password-reset`); },
    onSuccess: () => setNotice({ kind: 'ok', text: 'Lien de réinitialisation envoyé par e-mail.' }),
    onError: (err: any) => setNotice({ kind: 'error', text: apiErrorMessage(err, "Impossible d'envoyer le lien.") }),
  });

  const unlockMutation = useMutation({
    mutationFn: async (id: string) => { await apiClient.post(`/users/${id}/unlock`); },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });

  const updateRoleMutation = useMutation({
    mutationFn: async () => {
      if (!roleTarget) return;
      setRoleError('');
      await apiClient.put(`/users/${roleTarget.id}/role`, {
        role: newRole,
        academicTrackId: newTrackId || null,
        graduationYear: newRole === 'Etudiant' && newGraduationYear ? Number(newGraduationYear) : null
      });
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['users'] }); setRoleTarget(null); },
    onError: (err: any) => setRoleError(apiErrorMessage(err, 'Erreur lors de la modification du rôle.'))
  });

  const openRoleDialog = (u: UserDto) => {
    setRoleTarget(u); setNewRole(u.role); setNewTrackId(u.academicTrackId || ''); setRoleError('');
    setNewGraduationYear(u.graduationYear ? String(u.graduationYear) : '');
  };

  const resetForm = () => {
    setEmail(''); setFirstName(''); setLastName('');
    setRole('Etudiant'); setPassword(''); setErrorMessage('');
    setTrackId(''); setStudentNumber(''); setGraduationYear('');
  };

  const createDisabled = !email || !firstName || !lastName || !password
    || (needsTrack(role) && !trackId) || (role === 'Etudiant' && !studentNumber)
    || createUserMutation.isPending;

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

      {notice && (
        <div className={`p-3 rounded-lg text-sm flex justify-between items-center ${notice.kind === 'ok' ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-700'}`}>
          <span>{notice.text}</span>
          <button onClick={() => setNotice(null)} aria-label="Fermer" className="font-bold px-2">×</button>
        </div>
      )}

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
                  {(u.academicTrackCode || trackCode(u.academicTrackId)) && (
                    <div className="mt-2 text-xs text-gray-500 flex items-center gap-1">
                      <FontAwesomeIcon icon={faGraduationCap} />
                      Filière : <strong>{u.academicTrackCode || trackCode(u.academicTrackId)}</strong>
                      {u.graduationYear && <span className="ml-1">· Promotion {u.graduationYear}</span>}
                    </div>
                  )}
                  {needsTrack(u.role) && !u.academicTrackId && (
                    <div className="mt-2 text-xs text-amber-700 flex items-center gap-1">
                      <FontAwesomeIcon icon={faTriangleExclamation} /> Aucune filière : ce compte ne voit aucun dossier.
                    </div>
                  )}
                  {u.isLockedOut && (
                    <div className="mt-2 flex items-center justify-between gap-2 p-2 bg-red-50 border border-red-200 rounded-lg text-xs text-red-700">
                      <span><FontAwesomeIcon icon={faLock} className="mr-1" />Verrouillé (échecs de connexion)</span>
                      <button onClick={() => unlockMutation.mutate(u.id)} disabled={unlockMutation.isPending}
                        className="px-2 py-1 bg-white border border-red-300 hover:bg-red-100 rounded font-semibold">
                        <FontAwesomeIcon icon={faLockOpen} className="mr-1" />Déverrouiller
                      </button>
                    </div>
                  )}
                  <button onClick={() => openRoleDialog(u)}
                    className="mt-3 w-full flex items-center justify-center gap-1.5 px-3 py-1.5 border border-gray-200 hover:bg-gray-50 rounded-lg text-xs font-semibold text-gray-700">
                    <FontAwesomeIcon icon={faUserPen} /> Modifier le rôle / la filière
                  </button>
                  <div className="mt-1.5 grid grid-cols-2 gap-1.5">
                    <button onClick={() => resetLinkMutation.mutate(u.id)} disabled={!u.isActive || resetLinkMutation.isPending}
                      title="Envoie un lien de réinitialisation du mot de passe par e-mail"
                      className="flex items-center justify-center gap-1 px-2 py-1.5 border border-gray-200 hover:bg-gray-50 rounded-lg text-xs font-semibold text-gray-700 disabled:opacity-50">
                      <FontAwesomeIcon icon={faKey} /> Lien mot de passe
                    </button>
                    <button onClick={() => activeMutation.mutate({ id: u.id, isActive: !u.isActive })} disabled={activeMutation.isPending}
                      className={`flex items-center justify-center gap-1 px-2 py-1.5 border rounded-lg text-xs font-semibold ${u.isActive ? 'border-red-200 text-red-700 hover:bg-red-50' : 'border-green-200 text-green-700 hover:bg-green-50'}`}>
                      <FontAwesomeIcon icon={u.isActive ? faUserSlash : faUserCheck} /> {u.isActive ? 'Désactiver' : 'Réactiver'}
                    </button>
                  </div>
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
                disabled={createDisabled}
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
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block mb-1 font-semibold text-xs">
                Filière {needsTrack(role) ? '(obligatoire)' : '(optionnel)'} :
              </label>
              <select style={selectStyle} value={trackId} onChange={(e) => setTrackId(e.target.value)}>
                <option value="">-- Aucune --</option>
                {tracks.map(t => <option key={t.id} value={t.id}>{t.code} — {t.name}</option>)}
              </select>
            </div>
            {role === 'Etudiant' && (
              <div>
                <label className="block mb-1 font-semibold text-xs">Code Apogée :</label>
                <Input value={studentNumber} onInput={(e: any) => setStudentNumber(e.target.value)}
                  placeholder="Ex: E1452933" style={{ width: '100%' }} />
              </div>
            )}
            {role === 'Etudiant' && (
              <div>
                <label className="block mb-1 font-semibold text-xs">Promotion (année de sortie) :</label>
                <input type="number" min={2000} max={2100} value={graduationYear} onChange={(e) => setGraduationYear(e.target.value)}
                  placeholder="Ex: 2027" style={selectStyle} />
              </div>
            )}
          </div>
          <div>
            <label className="block mb-1 font-semibold text-xs">Mot de passe initial :</label>
            <Input type="Password" value={password} onInput={(e: any) => setPassword(e.target.value)} style={{ width: '100%' }} />
            <div className="text-xs text-gray-400 mt-1">Min. 8 caractères, 1 majuscule, 1 minuscule, 1 chiffre, 1 caractère spécial</div>
          </div>
        </div>
      </Dialog>

      {/* Role dialog */}
      {roleTarget && (
        <Dialog open headerText={`Rôle de ${roleTarget.firstName} ${roleTarget.lastName}`}
          footer={
            <Bar endContent={
              <>
                <Button design="Transparent" onClick={() => setRoleTarget(null)}>Annuler</Button>
                <Button design="Emphasized" onClick={() => updateRoleMutation.mutate()}
                  disabled={(needsTrack(newRole) && !newTrackId) || updateRoleMutation.isPending}>
                  {updateRoleMutation.isPending ? 'Enregistrement...' : 'Enregistrer'}
                </Button>
              </>
            } />
          }
        >
          <div className="flex flex-col gap-4 p-4" style={{ minWidth: '380px' }}>
            {roleError && (
              <div className="p-2.5 bg-red-50 text-red-700 rounded-lg text-sm">
                <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {roleError}
              </div>
            )}
            <div>
              <label className="block mb-1 font-semibold text-xs">Rôle :</label>
              <select style={selectStyle} value={newRole} onChange={(e) => setNewRole(e.target.value)}>
                {Object.entries(ROLE_CONFIG).map(([r, cfg]) => <option key={r} value={r}>{cfg.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block mb-1 font-semibold text-xs">Filière {needsTrack(newRole) ? '(obligatoire)' : '(optionnel)'} :</label>
              <select style={selectStyle} value={newTrackId} onChange={(e) => setNewTrackId(e.target.value)}>
                <option value="">-- Aucune --</option>
                {tracks.map(t => <option key={t.id} value={t.id}>{t.code} — {t.name}</option>)}
              </select>
            </div>
            {newRole === 'Etudiant' && (
              <div>
                <label className="block mb-1 font-semibold text-xs">Promotion (année de sortie) :</label>
                <input type="number" min={2000} max={2100} value={newGraduationYear} onChange={(e) => setNewGraduationYear(e.target.value)}
                  placeholder="Ex: 2027" style={selectStyle} />
              </div>
            )}
            <div className="text-xs text-gray-500">
              Les sessions ouvertes de cet utilisateur seront fermées : il devra se reconnecter pour obtenir ses nouveaux droits.
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
};
export default UsersPage;
