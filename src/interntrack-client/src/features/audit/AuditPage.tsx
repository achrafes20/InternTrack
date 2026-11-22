import React, { useState, useMemo, useDeferredValue } from 'react';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { Text, BusyIndicator, Title, Tag } from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faShieldHalved, faFilter, faRotateLeft, faSearch,
  faUser, faChevronLeft, faChevronRight, faDatabase,
  faCube, faCalendar
} from '@fortawesome/free-solid-svg-icons';
import { apiClient } from '../../api/client';
import { AuditLogDto, PaginatedList } from '../../types';

const getActionStyle = (action: string): { borderColor: string; bg: string; tagScheme: string; label: string } => {
  const a = (action || '').toLowerCase();
  // Actions enregistrées par le journal : Added / Modified / Deleted (états EF Core).
  if (a === 'added') return { borderColor: '#107e3e', bg: '#f0fdf4', tagScheme: '8', label: 'CRÉATION' };
  if (a === 'modified') return { borderColor: '#f5a623', bg: '#fffbeb', tagScheme: '6', label: 'MODIFICATION' };
  if (a === 'deleted') return { borderColor: '#e53935', bg: '#fff5f5', tagScheme: '2', label: 'SUPPRESSION' };
  if (a.includes('create') || a.includes('insert') || a.includes('propose'))
    return { borderColor: '#107e3e', bg: '#f0fdf4', tagScheme: '8', label: 'CRÉATION' };
  if (a.includes('approve') || a.includes('validate') || a.includes('valide') || a.includes('start'))
    return { borderColor: '#0070f3', bg: '#eff6ff', tagScheme: '5', label: 'VALIDATION' };
  if (a.includes('reject') || a.includes('refuse') || a.includes('delete'))
    return { borderColor: '#e53935', bg: '#fff5f5', tagScheme: '2', label: 'REFUS/SUPPRESSION' };
  if (a.includes('login') || a.includes('logout') || a.includes('auth'))
    return { borderColor: '#6b7280', bg: '#f9fafb', tagScheme: '9', label: 'AUTHENTIFICATION' };
  if (a.includes('update') || a.includes('modify'))
    return { borderColor: '#f5a623', bg: '#fffbeb', tagScheme: '6', label: 'MODIFICATION' };
  return { borderColor: '#8b5cf6', bg: '#faf5ff', tagScheme: '1', label: action?.toUpperCase() || 'ACTION' };
};

export const AuditPage: React.FC = () => {
  const [filterAction, setFilterAction] = useState('');
  const [filterEntity, setFilterEntity] = useState('');
  const [filterUser, setFilterUser] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Filtres et pagination exécutés côté serveur : l'ensemble du journal est consultable,
  // pas seulement la première page. Les saisies texte sont différées pour limiter les requêtes.
  const deferredAction = useDeferredValue(filterAction);
  const deferredEntity = useDeferredValue(filterEntity);
  const deferredUser = useDeferredValue(filterUser);

  const params = useMemo(() => ({
    action: deferredAction || undefined,
    entityName: deferredEntity || undefined,
    user: deferredUser || undefined,
    from: dateFrom ? `${dateFrom}T00:00:00Z` : undefined,
    to: dateTo ? `${dateTo}T23:59:59Z` : undefined,
    pageNumber: page,
    pageSize,
  }), [deferredAction, deferredEntity, deferredUser, dateFrom, dateTo, page]);

  const { data: response, isLoading } = useQuery<PaginatedList<AuditLogDto>>({
    queryKey: ['audit-logs', params],
    queryFn: async () => (await apiClient.get('/audit', { params })).data,
    placeholderData: keepPreviousData,
  });

  // Un filtre a changé : retour à la première page (ajustement pendant le rendu, sans effet en cascade).
  const filtersKey = [deferredAction, deferredEntity, deferredUser, dateFrom, dateTo].join('|');
  const [lastFiltersKey, setLastFiltersKey] = useState(filtersKey);
  if (filtersKey !== lastFiltersKey) {
    setLastFiltersKey(filtersKey);
    setPage(1);
  }

  const paged: AuditLogDto[] = response?.items ?? [];
  const totalPages = response?.totalPages ?? 0;
  const totalCount = response?.totalCount ?? 0;
  const hasFilters = filterAction || filterEntity || filterUser || dateFrom || dateTo;

  const resetFilters = () => {
    setFilterAction(''); setFilterEntity(''); setFilterUser('');
    setDateFrom(''); setDateTo(''); setPage(1);
  };


  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="bg-gradient-to-r from-ensa-navy to-blue-700 rounded-2xl p-6 text-white">
        <div className="flex items-center gap-3 mb-2">
          <FontAwesomeIcon icon={faShieldHalved} className="text-2xl opacity-90" />
          <div className="text-2xl font-bold">Journal d'Audit Immuable</div>
        </div>
        <div className="opacity-85 text-sm mb-3">
          Table append-only protégée par trigger PostgreSQL — traçabilité absolue et inaltérable de chaque action système.
        </div>
        <div className="flex gap-3 flex-wrap">
          <span className="bg-white/15 rounded-full px-3 py-1 text-xs flex items-center gap-1.5">
            <FontAwesomeIcon icon={faDatabase} />
            {totalCount} événement(s) correspondant(s)
          </span>
          <span className="bg-white/15 rounded-full px-3 py-1 text-xs flex items-center gap-1.5">
            <FontAwesomeIcon icon={faFilter} />
            {paged.length} sur cette page
          </span>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-2xl p-5 border border-gray-200/80 shadow-sm flex flex-col gap-4">
        <div className="flex items-center justify-between flex-wrap gap-2">
          <div className="flex items-center gap-2 font-bold text-sm text-gray-800">
            <FontAwesomeIcon icon={faFilter} className="text-blue-600" />
            Filtres de recherche multicritères
          </div>
          {hasFilters && (
            <button
              onClick={resetFilters}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg text-xs font-semibold transition-all"
            >
              <FontAwesomeIcon icon={faRotateLeft} />
              Réinitialiser les filtres
            </button>
          )}
        </div>

        <div className="grid gap-3" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))' }}>
          <div>
            <label className="flex items-center gap-1 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faShieldHalved} className="text-blue-500 text-xs" />
              Action
            </label>
            <input
              type="text"
              value={filterAction}
              onChange={(e) => { setFilterAction(e.target.value); setPage(1); }}
              placeholder="Ex: Create, Approve..."
              className="w-full px-3.5 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
            />
          </div>

          <div>
            <label className="flex items-center gap-1 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faCube} className="text-blue-500 text-xs" />
              Entité
            </label>
            <input
              type="text"
              value={filterEntity}
              onChange={(e) => { setFilterEntity(e.target.value); setPage(1); }}
              placeholder="Ex: Internship, Defense..."
              className="w-full px-3.5 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
            />
          </div>

          <div>
            <label className="flex items-center gap-1 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faUser} className="text-blue-500 text-xs" />
              Utilisateur
            </label>
            <input
              type="text"
              value={filterUser}
              onChange={(e) => { setFilterUser(e.target.value); setPage(1); }}
              placeholder="Ex: admin@ensa.ma"
              className="w-full px-3.5 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
            />
          </div>

          <div>
            <label className="flex items-center gap-1 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faCalendar} className="text-blue-500 text-xs" />
              Date début
            </label>
            <input
              type="date"
              value={dateFrom}
              onChange={(e) => { setDateFrom(e.target.value); setPage(1); }}
              className="w-full px-3.5 py-2 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border cursor-pointer"
            />
          </div>

          <div>
            <label className="flex items-center gap-1 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faCalendar} className="text-blue-500 text-xs" />
              Date fin
            </label>
            <input
              type="date"
              value={dateTo}
              onChange={(e) => { setDateTo(e.target.value); setPage(1); }}
              className="w-full px-3.5 py-2 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border cursor-pointer"
            />
          </div>
        </div>
      </div>

      {/* Log entries */}
      {isLoading ? (
        <div className="flex justify-center py-16"><BusyIndicator active /></div>
      ) : totalCount === 0 ? (
        <div className="text-center py-16 bg-gray-50 rounded-2xl border-2 border-dashed border-gray-300">
          <FontAwesomeIcon icon={faSearch} className="text-5xl text-gray-300 mb-3" />
          <Title level="H4">Aucun événement trouvé</Title>
          <Text style={{ color: '#6b7280' }}>Modifiez les filtres pour afficher des résultats.</Text>
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {paged.map(l => {
            const style = getActionStyle(l.action);
            const ts = l.timestamp ? new Date(l.timestamp) : null;
            return (
              <div key={l.id}
                style={{ background: style.bg, borderLeft: `4px solid ${style.borderColor}`, gridTemplateColumns: '140px 160px auto 1fr', gap: '12px', alignItems: 'center', boxShadow: '0 1px 3px rgba(0,0,0,0.05)', fontSize: '13px', borderRadius: '8px', padding: '12px 16px', display: 'grid' }}
              >
                {/* Timestamp */}
                <div>
                  <div className="font-mono font-semibold text-gray-700">
                    {ts ? ts.toLocaleDateString('fr-FR') : '-'}
                  </div>
                  <div className="font-mono text-gray-400 text-xs">
                    {ts ? ts.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : ''}
                  </div>
                </div>
                {/* User */}
                <div className="font-semibold text-gray-800 overflow-hidden text-ellipsis whitespace-nowrap flex items-center gap-1">
                  <FontAwesomeIcon icon={faUser} className="text-gray-400 flex-shrink-0" />
                  {l.userEmail || l.userId || 'Système'}
                </div>
                {/* Action badge */}
                <div>
                  <Tag colorScheme={style.tagScheme as any}>{style.label}</Tag>
                  <div className="text-gray-400 text-xs mt-1">{l.action}</div>
                </div>
                {/* Entity */}
                <div>
                  <div className="font-semibold text-gray-800">
                    {l.entityName}
                    <span className="font-normal text-gray-400 text-xs ml-1">#{(l.entityId || '').substring(0, 8)}…</span>
                  </div>
                  {l.changes && (
                    <div className="mt-1 font-mono text-xs text-gray-500 bg-black/5 rounded px-2 py-0.5 overflow-hidden text-ellipsis whitespace-nowrap max-w-full">
                      {l.changes}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex justify-center items-center gap-3 flex-wrap">
          <button
            disabled={page <= 1} onClick={() => setPage(p => p - 1)}
            className="flex items-center gap-1.5 px-4 py-2 border border-gray-300 rounded-lg text-sm disabled:opacity-40 disabled:cursor-default hover:bg-gray-50"
          >
            <FontAwesomeIcon icon={faChevronLeft} /> Précédent
          </button>
          <span className="text-sm text-gray-500">
            Page {page} / {totalPages} ({totalCount} résultats)
          </span>
          <button
            disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}
            className="flex items-center gap-1.5 px-4 py-2 border border-gray-300 rounded-lg text-sm disabled:opacity-40 disabled:cursor-default hover:bg-gray-50"
          >
            Suivant <FontAwesomeIcon icon={faChevronRight} />
          </button>
        </div>
      )}
    </div>
  );
};
export default AuditPage;
