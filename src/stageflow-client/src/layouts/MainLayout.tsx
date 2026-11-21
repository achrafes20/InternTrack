import React, { useState } from 'react';
import { ChangePasswordDialog } from '../features/auth/ChangePasswordDialog';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faTableColumns, faBriefcase, faFolderOpen, faFileLines,
  faGraduationCap, faClipboardList, faUsers, faRightFromBracket, faKey, faTriangleExclamation
} from '@fortawesome/free-solid-svg-icons';

const NAV_ICONS: Record<string, any> = {
  '/dashboard':   faTableColumns,
  '/offers':      faBriefcase,
  '/internships': faFolderOpen,
  '/reports':     faFileLines,
  '/defenses':    faGraduationCap,
  '/anomalies':   faTriangleExclamation,
  '/audit':       faClipboardList,
  '/users':       faUsers,
};

const ROLE_COLORS: Record<string, string> = {
  Admin:              'bg-red-100 text-red-700',
  ResponsableFiliere: 'bg-amber-100 text-amber-700',
  Encadrant:          'bg-violet-100 text-violet-700',
  Etudiant:           'bg-blue-100 text-blue-700',
};

const ROLE_LABELS: Record<string, string> = {
  Admin:              'Administrateur',
  ResponsableFiliere: 'Responsable de Filière',
  Encadrant:          'Encadrant Pédagogique',
  Etudiant:           'Élève Ingénieur',
};

export const MainLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [showPasswordDialog, setShowPasswordDialog] = useState(false);

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const navItems = [
    { path: '/dashboard',   label: 'Tableau de bord',                                              roles: ['Admin', 'ResponsableFiliere', 'Encadrant', 'Etudiant'] },
    { path: '/offers',      label: 'Offres de stage',                                              roles: ['Admin', 'ResponsableFiliere', 'Encadrant', 'Etudiant'] },
    { path: '/internships', label: user?.role === 'Etudiant' ? 'Mes Stages & PFE' : 'Gestion des Stages', roles: ['Admin', 'ResponsableFiliere', 'Encadrant', 'Etudiant'] },
    { path: '/reports',     label: 'Rapports & Suivi',                                             roles: ['Admin', 'ResponsableFiliere', 'Encadrant', 'Etudiant'] },
    { path: '/defenses',    label: 'Soutenances',                                                  roles: ['Admin', 'ResponsableFiliere', 'Encadrant', 'Etudiant'] },
    { path: '/anomalies',   label: 'Anomalies',                                                  roles: ['Admin', 'ResponsableFiliere'] },
    { path: '/audit',       label: "Journal d'audit",                                              roles: ['Admin'] },
    { path: '/users',       label: 'Utilisateurs',                                                 roles: ['Admin'] },
  ];

  const allowedNav = navItems.filter(item => user && item.roles.includes(user.role));
  const roleLabel  = ROLE_LABELS[user?.role || ''] || user?.role || '';
  const roleColor  = ROLE_COLORS[user?.role || ''] || 'bg-gray-100 text-gray-700';
  const initials   = `${(user?.firstName || '')[0] || ''}${(user?.lastName || '')[0] || ''}`.toUpperCase();

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">

      {/* Top bar */}
      <header className="bg-gradient-to-r from-ensa-navy via-ensa-blue to-sky-500 text-white px-6 py-3 flex items-center justify-between shadow-md">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 bg-white rounded-xl flex items-center justify-center shadow">
            <span className="text-sm font-black text-blue-600 tracking-tight">SF</span>
          </div>
          <div>
            <div className="font-bold text-base leading-tight">StageFlow</div>
            <div className="text-xs opacity-75">ENSA · Portail des Stages & PFE</div>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="hidden sm:flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-white/20 flex items-center justify-center text-sm font-bold">
              {initials}
            </div>
            <div className="text-right leading-tight">
              <div className="text-sm font-semibold">{user?.firstName} {user?.lastName}</div>
              <div className="text-xs opacity-75">{roleLabel}</div>
            </div>
          </div>
          <button
            onClick={() => setShowPasswordDialog(true)}
            title="Changer mon mot de passe"
            className="flex items-center gap-2 bg-white/15 hover:bg-white/25 border border-white/30 text-white rounded-lg px-3 py-1.5 text-sm font-semibold transition-all"
          >
            <FontAwesomeIcon icon={faKey} className="text-xs" />
            <span className="hidden md:inline">Mot de passe</span>
          </button>
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 bg-white/15 hover:bg-white/25 border border-white/30 text-white rounded-lg px-3 py-1.5 text-sm font-semibold transition-all"
          >
            <FontAwesomeIcon icon={faRightFromBracket} className="text-xs" />
            Déconnexion
          </button>
        </div>
      </header>

      {/* Navigation */}
      <nav className="bg-white border-b border-slate-200 shadow-sm px-4 flex items-center gap-1 overflow-x-auto">
        {allowedNav.map(item => {
          const isActive = location.pathname === item.path;
          return (
            <button
              key={item.path}
              onClick={() => navigate(item.path)}
              className={`flex items-center gap-2 px-4 py-3 text-sm font-medium whitespace-nowrap border-b-2 transition-all
                ${isActive
                  ? 'border-blue-600 text-blue-600 bg-blue-50'
                  : 'border-transparent text-slate-600 hover:text-blue-600 hover:bg-slate-50'
                }`}
            >
              <FontAwesomeIcon icon={NAV_ICONS[item.path]} className={isActive ? 'text-blue-600' : 'text-slate-400'} />
              {item.label}
            </button>
          );
        })}
        <div className={`ml-3 px-3 py-1 rounded-full text-xs font-semibold ${roleColor} hidden md:block my-auto`}>
          {roleLabel}
        </div>
      </nav>

      {/* Main content */}
      <main className="flex-1 max-w-screen-xl mx-auto w-full px-6 py-7 box-border">
        <Outlet />
      </main>

      {showPasswordDialog && <ChangePasswordDialog onClose={() => setShowPasswordDialog(false)} />}

      {/* Footer */}
      <footer className="bg-white border-t border-slate-200 text-center py-3 text-xs text-slate-400">
        StageFlow &copy; {new Date().getFullYear()} &middot; ENSA &middot; Portail de Gestion des Stages &amp; PFE
      </footer>
    </div>
  );
};
