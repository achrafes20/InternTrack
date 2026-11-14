import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faFolderOpen, faClock, faBolt, faCircleCheck,
  faChartBar, faBuilding, faCalendarCheck, faShieldHalved,
  faFileLines, faUsers, faGraduationCap, faBriefcase,
  faCheckCircle
} from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { useAuth } from '../../context/AuthContext';
import { DashboardStats } from '../../types';

interface KpiProps { icon: any; value: number; label: string; colorClass: string; bgClass: string; borderClass: string; }
const KpiCard: React.FC<KpiProps> = ({ icon, value, label, colorClass, bgClass, borderClass }) => (
  <div className={`bg-white rounded-xl p-5 shadow-card flex items-center gap-4 border-l-4 ${borderClass}`}>
    <div className={`w-12 h-12 rounded-xl ${bgClass} flex items-center justify-center flex-shrink-0`}>
      <FontAwesomeIcon icon={icon} className={`text-xl ${colorClass}`} />
    </div>
    <div>
      <div className={`text-3xl font-extrabold ${colorClass} leading-none`}>{value}</div>
      <div className="text-xs text-slate-500 mt-1 font-medium">{label}</div>
    </div>
  </div>
);

const getDaysUntil = (d: string) => Math.ceil((new Date(d).getTime() - Date.now()) / 86400000);

const QUICK_ACTIONS: Record<string, { label: string; path: string; icon: any; color: string }[]> = {
  Etudiant: [
    { label: 'Nouveau dossier de stage', path: '/internships', icon: faFolderOpen,     color: 'blue'   },
    { label: 'Consulter les offres',     path: '/offers',      icon: faBriefcase,      color: 'green'  },
    { label: 'Soumettre un rapport',     path: '/reports',     icon: faFileLines,      color: 'violet' },
    { label: 'Ma soutenance',            path: '/defenses',    icon: faGraduationCap,  color: 'amber'  },
  ],
  ResponsableFiliere: [
    { label: 'Dossiers à valider',       path: '/internships', icon: faFolderOpen,     color: 'green'  },
    { label: 'Planifier une soutenance', path: '/defenses',    icon: faGraduationCap,  color: 'violet' },
    { label: 'Rapports en attente',      path: '/reports',     icon: faFileLines,      color: 'amber'  },
  ],
  Encadrant: [
    { label: 'Rapports en attente',  path: '/reports',     icon: faFileLines,     color: 'amber'  },
    { label: 'Planning soutenances', path: '/defenses',    icon: faGraduationCap, color: 'violet' },
    { label: 'Dossiers suivis',      path: '/internships', icon: faFolderOpen,    color: 'blue'   },
  ],
  Admin: [
    { label: 'Gérer les utilisateurs', path: '/users',       icon: faUsers,          color: 'red'    },
    { label: "Journal d'audit",        path: '/audit',       icon: faShieldHalved,   color: 'slate'  },
    { label: 'Tous les stages',        path: '/internships', icon: faFolderOpen,     color: 'blue'   },
    { label: 'Soutenances',            path: '/defenses',    icon: faGraduationCap,  color: 'violet' },
  ],
};

const ACTION_COLORS: Record<string, string> = {
  blue:   'bg-blue-50 hover:bg-blue-100 text-blue-700 border-blue-200',
  green:  'bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border-emerald-200',
  violet: 'bg-violet-50 hover:bg-violet-100 text-violet-700 border-violet-200',
  amber:  'bg-amber-50 hover:bg-amber-100 text-amber-700 border-amber-200',
  red:    'bg-red-50 hover:bg-red-100 text-red-700 border-red-200',
  slate:  'bg-slate-50 hover:bg-slate-100 text-slate-700 border-slate-200',
};

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Administrateur Système',
  ResponsableFiliere: 'Responsable de Filière PFE',
  Encadrant: 'Encadrant Pédagogique',
  Etudiant: 'Élève Ingénieur (5A)',
};

export const DashboardPage: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();

  const { data: stats, isLoading } = useQuery<DashboardStats>({
    queryKey: ['dashboard-stats'],
    queryFn: async () => (await api.get('/dashboard/stats')).data,
  });

  if (isLoading) return (
    <div className="flex items-center justify-center h-64 text-slate-400">
      <FontAwesomeIcon icon={faClock} spin className="mr-2" /> Chargement des indicateurs...
    </div>
  );
  if (!stats) return <div className="text-slate-400">Données indisponibles.</div>;

  const quickActions = QUICK_ACTIONS[user?.role || ''] || [];

  return (
    <div className="flex flex-col gap-6">

      {/* Bannière de bienvenue */}
      <div className="rounded-2xl bg-gradient-to-r from-ensa-navy via-ensa-blue to-sky-500 text-white px-8 py-7 flex flex-wrap gap-5 items-center justify-between shadow-elevated">
        <div>
          <span className="inline-block bg-white/20 text-xs font-semibold rounded-full px-3 py-1 mb-3 tracking-wide">
            {ROLE_LABELS[user?.role || ''] || user?.role}
          </span>
          <h1 className="text-2xl font-extrabold mb-1">Bonjour, {user?.firstName} {user?.lastName}</h1>
          <p className="text-sm opacity-85">
            Portail institutionnel de gestion et traçabilité des stages & PFE — ENSA
          </p>
        </div>
        <div className="flex gap-3 flex-wrap">
          <button onClick={() => navigate('/internships')}
            className="flex items-center gap-2 bg-white text-blue-700 hover:bg-blue-50 font-bold px-5 py-2.5 rounded-xl text-sm transition shadow-sm active:scale-95">
            <FontAwesomeIcon icon={faFolderOpen} />
            Mes dossiers
          </button>
          <button onClick={() => navigate('/offers')}
            className="flex items-center gap-2 bg-white/20 hover:bg-white/30 text-white font-bold px-5 py-2.5 rounded-xl text-sm transition border border-white/40 backdrop-blur-sm shadow-sm active:scale-95">
            <FontAwesomeIcon icon={faBriefcase} />
            Offres de stage
          </button>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <KpiCard icon={faFolderOpen}   value={stats.totalInternships}  label="Total des stages"         colorClass="text-blue-600"    bgClass="bg-blue-50"    borderClass="border-blue-500" />
        <KpiCard icon={faClock}        value={stats.pendingValidation}  label="En attente de validation" colorClass="text-amber-600"   bgClass="bg-amber-50"   borderClass="border-amber-400" />
        <KpiCard icon={faBolt}         value={stats.inProgress}         label="Stages en cours"          colorClass="text-emerald-600" bgClass="bg-emerald-50" borderClass="border-emerald-500" />
        <KpiCard icon={faCircleCheck}  value={stats.completed}          label="Stages terminés"          colorClass="text-slate-500"   bgClass="bg-slate-100"  borderClass="border-slate-400" />
      </div>

      {/* Actions rapides */}
      {quickActions.length > 0 && (
        <div className="bg-white rounded-xl p-5 shadow-card">
          <h3 className="text-sm font-bold text-slate-700 mb-4 flex items-center gap-2">
            <FontAwesomeIcon icon={faBolt} className="text-amber-500" /> Actions rapides
          </h3>
          <div className="flex flex-wrap gap-3">
            {quickActions.map(a => (
              <button key={a.path + a.label} onClick={() => navigate(a.path)}
                className={`flex items-center gap-2 px-4 py-2.5 rounded-lg border text-sm font-semibold transition ${ACTION_COLORS[a.color]}`}>
                <FontAwesomeIcon icon={a.icon} className="text-xs" />
                {a.label}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Stats grid */}
      <div className="grid md:grid-cols-2 gap-5">

        {/* Par filière */}
        <div className="bg-white rounded-xl p-5 shadow-card">
          <h3 className="text-sm font-bold text-slate-700 mb-4 flex items-center gap-2">
            <FontAwesomeIcon icon={faChartBar} className="text-blue-500" /> Répartition par filière
          </h3>
          {stats.internshipsByTrack.length === 0
            ? <p className="text-sm text-slate-400">Aucune donnée disponible.</p>
            : stats.internshipsByTrack.map(t => {
                const pct = stats.totalInternships > 0 ? Math.round(t.count / stats.totalInternships * 100) : 0;
                return (
                  <div key={t.trackCode} className="mb-3">
                    <div className="flex justify-between text-sm mb-1">
                      <span className="font-semibold text-slate-700">{t.trackCode} — {t.trackName}</span>
                      <span className="text-blue-600 font-bold">{t.count}</span>
                    </div>
                    <div className="h-1.5 bg-slate-100 rounded-full overflow-hidden">
                      <div className="h-full bg-blue-500 rounded-full transition-all" style={{ width: `${pct}%` }} />
                    </div>
                  </div>
                );
              })
          }
        </div>

        {/* Top entreprises */}
        <div className="bg-white rounded-xl p-5 shadow-card">
          <h3 className="text-sm font-bold text-slate-700 mb-4 flex items-center gap-2">
            <FontAwesomeIcon icon={faBuilding} className="text-emerald-500" /> Top entreprises partenaires
          </h3>
          {stats.topCompanies.length === 0
            ? <p className="text-sm text-slate-400">Aucune entreprise enregistrée.</p>
            : stats.topCompanies.map((c, i) => (
                <div key={c.companyName} className="flex justify-between items-center py-2.5 border-b border-slate-50 text-sm">
                  <div className="flex items-center gap-3">
                    <span className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold text-white
                      ${i === 0 ? 'bg-amber-400' : i === 1 ? 'bg-slate-400' : 'bg-orange-400'}`}>{i + 1}</span>
                    <span className="text-slate-700">{c.companyName}</span>
                  </div>
                  <span className="text-emerald-600 font-bold">{c.count} conv.</span>
                </div>
              ))
          }
        </div>
      </div>

      {/* Prochaines soutenances */}
      <div className="bg-white rounded-xl p-5 shadow-card">
        <h3 className="text-sm font-bold text-slate-700 mb-4 flex items-center gap-2">
          <FontAwesomeIcon icon={faCalendarCheck} className="text-violet-500" /> Prochaines soutenances (14 jours)
        </h3>
        {stats.upcomingDefenses.length === 0
          ? <p className="text-center text-slate-400 py-6 text-sm">Aucune soutenance programmée sur cette période.</p>
          : stats.upcomingDefenses.map(d => {
              const days = getDaysUntil(d.scheduledAt);
              return (
                <div key={d.id} className="flex flex-wrap justify-between items-center py-3 border-b border-slate-50 gap-3">
                  <div>
                    <div className="font-semibold text-slate-800 text-sm">{d.internshipTitle}</div>
                    <div className="text-xs text-slate-500 mt-0.5">{d.studentName}</div>
                  </div>
                  <div className="text-right">
                    <div className="text-sm font-bold text-blue-600">
                      {new Date(d.scheduledAt).toLocaleDateString('fr-FR')} — {new Date(d.scheduledAt).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}
                    </div>
                    <div className="text-xs text-slate-500">Salle : {d.classroom}</div>
                    <span className={`text-xs font-semibold px-2 py-0.5 rounded-full mt-1 inline-block
                      ${days <= 2 ? 'bg-red-100 text-red-600' : 'bg-emerald-100 text-emerald-600'}`}>
                      {days <= 0 ? "Aujourd'hui" : days === 1 ? 'Demain' : `Dans ${days} jours`}
                    </span>
                  </div>
                </div>
              );
            })
        }
      </div>

      {/* Statut système — Admin uniquement */}
      {user?.role === 'Admin' && (
        <div className="bg-white rounded-xl p-5 shadow-card">
          <h3 className="text-sm font-bold text-slate-700 mb-4 flex items-center gap-2">
            <FontAwesomeIcon icon={faShieldHalved} className="text-slate-500" /> État du système
          </h3>
          <div className="flex flex-wrap gap-3">
            {['API Backend .NET 10', 'Base de données PostgreSQL', 'Génération PDF (QuestPDF)', "Service e-mail (mode dev)", 'Résumé IA (Gemini)'].map(s => (
              <div key={s} className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 text-emerald-700 rounded-lg px-3 py-2 text-xs font-medium">
                <FontAwesomeIcon icon={faCheckCircle} className="text-emerald-500" />
                {s}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
export default DashboardPage;
