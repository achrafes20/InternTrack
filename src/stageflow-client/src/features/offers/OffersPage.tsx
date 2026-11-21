import React, { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Card, Input, Title, Text, Button, Tag, Dialog, TextArea } from '@ui5/webcomponents-react';
import { useNavigate } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faBriefcase, faLocationDot, faClock, faCoins, faSearch,
  faRotateLeft, faMagnifyingGlass, faCheck, faArrowRight,
  faTriangleExclamation, faBuilding, faGraduationCap, faPlus
} from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { useAuth } from '../../context/AuthContext';
import { InternshipOffer, apiErrorMessage } from '../../types';
import { CreateOfferDialog, CompaniesDialog } from './OfferManagementDialogs';

const TRACK_COLORS: Record<string, string> = {
  GI: '#2563eb', GC: '#059669', GE: '#7c3aed',
  GIM: '#d97706', INFO: '#0891b2', DEFAULT: '#6b7280',
};
const getTrackColor = (code: string) => TRACK_COLORS[code?.toUpperCase()] || TRACK_COLORS.DEFAULT;

export const OffersPage: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [city, setCity] = useState('');
  const [trackFilter, setTrackFilter] = useState('');

  const isCoord = user?.role === 'ResponsableFiliere';
  const isAdmin = user?.role === 'Admin';
  const [openCreateOffer, setOpenCreateOffer] = useState(false);
  const [openCreateCompany, setOpenCreateCompany] = useState(false);
  const [editingOffer, setEditingOffer] = useState<InternshipOffer | null>(null);
  const [manageError, setManageError] = useState('');

  // Un responsable gère les offres de sa filière ; l'administrateur toutes les offres.
  const canManage = (o: InternshipOffer) => isAdmin || (isCoord && o.academicTrackId === user?.academicTrackId);
  const isExpired = (o: InternshipOffer) => new Date(o.expirationDate) < new Date();

  const offerState = (o: InternshipOffer) => !o.isActive
    ? <span className="px-2.5 py-1 bg-gray-100 text-gray-600 rounded-xl text-xs font-semibold">Désactivée</span>
    : isExpired(o)
    ? <span className="px-2.5 py-1 bg-amber-100 text-amber-800 rounded-xl text-xs font-semibold">Expirée</span>
    : <span className="px-2.5 py-1 bg-green-100 text-green-800 rounded-xl text-xs font-semibold"><FontAwesomeIcon icon={faCheck} className="mr-1" />Active</span>;

  const toggleActiveMutation = useMutation({
    mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) => {
      setManageError('');
      await api.put(`/offers/${id}/active`, { isActive });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['offers'] }),
    onError: (err: any) => setManageError(apiErrorMessage(err, "Impossible de modifier l'état de l'offre.")),
  });

  const [selectedOffer, setSelectedOffer] = useState<InternshipOffer | null>(null);
  const [postulerTitle, setPostulerTitle] = useState('');
  const [postulerDesc, setPostulerDesc] = useState('');
  const [supervisorName, setSupervisorName] = useState('');
  const [supervisorEmail, setSupervisorEmail] = useState('');
  const [supervisorPhone, setSupervisorPhone] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [applyError, setApplyError] = useState('');

  const { data, isLoading } = useQuery<{ items: InternshipOffer[]; totalCount: number }>({
    queryKey: ['offers', isCoord || isAdmin],
    // Les gestionnaires voient aussi les offres désactivées ou expirées, pour pouvoir les réactiver.
    queryFn: async () => (await api.get('/offers', { params: { pageSize: 100, includeClosed: isCoord || isAdmin } })).data,
  });

  const allOffers = data?.items || [];
  const uniqueTracks = useMemo(() => [...new Set(allOffers.map(o => o.academicTrackCode).filter(Boolean))], [allOffers]);

  const filtered = useMemo(() =>
    allOffers.filter(o => {
      const matchSearch = !search || (o.title + o.description + (o.requiredSkills || '')).toLowerCase().includes(search.toLowerCase());
      const matchCity = !city || (o.city || '').toLowerCase().includes(city.toLowerCase());
      const matchTrack = !trackFilter || o.academicTrackCode === trackFilter;
      return matchSearch && matchCity && matchTrack;
    }), [allOffers, search, city, trackFilter]);

  const hasFilters = search || city || trackFilter;

  const applyMutation = useMutation({
    mutationFn: async () => {
      if (!selectedOffer) return;
      setApplyError('');
      await api.post('/internships', {
        title: postulerTitle, subjectDescription: postulerDesc,
        companyId: selectedOffer.companyId, academicTrackId: selectedOffer.academicTrackId,
        companySupervisorName: supervisorName, companySupervisorEmail: supervisorEmail,
        companySupervisorPhone: supervisorPhone || null, internshipOfferId: selectedOffer.id,
        startDate: new Date(startDate).toISOString(), endDate: new Date(endDate).toISOString(),
      });
    },
    onSuccess: () => { setSelectedOffer(null); queryClient.invalidateQueries({ queryKey: ['internships'] }); navigate('/internships'); },
    onError: (err: any) => { setApplyError(apiErrorMessage(err, 'Erreur lors de la soumission du dossier.')); },
  });

  const handleOpenApply = (offer: InternshipOffer) => {
    setSelectedOffer(offer);
    setPostulerTitle(`Stage PFE – ${offer.title}`);
    setPostulerDesc(offer.description);
    // Début dans une semaine, fin calée sur la durée annoncée par l'offre (contrôlée par le serveur).
    const start = new Date(); start.setDate(start.getDate() + 7);
    const end = new Date(start); end.setDate(end.getDate() + offer.durationInWeeks * 7);
    setStartDate(start.toISOString().substring(0, 10));
    setEndDate(end.toISOString().substring(0, 10));
    setApplyError('');
  };

  const inputStyle = {
    width: '100%', padding: '7px 10px', borderRadius: '6px',
    border: '1px solid #d1d5db', fontSize: '14px', boxSizing: 'border-box' as const
  };

  return (
    <div className="flex flex-col gap-5">
      {/* Header */}
      <div className="bg-gradient-to-r from-ensa-navy via-ensa-blue to-sky-500 rounded-2xl p-7 text-white shadow-elevated">
        <div className="flex items-center gap-3 mb-2">
          <FontAwesomeIcon icon={faBriefcase} className="text-3xl opacity-90" />
          <div>
            <div className="text-2xl font-extrabold">Catalogue des Offres de Stage &amp; PFE</div>
            <div className="opacity-85 text-sm mt-1">
              Offres validées par l'ENSA disponibles pour les élèves ingénieurs — {allOffers.length} offre(s) disponible(s)
            </div>
          </div>
        </div>
        {(isCoord || isAdmin) && (
          <div className="flex gap-2 mt-4 flex-wrap">
            <button onClick={() => setOpenCreateOffer(true)}
              className="flex items-center gap-2 px-4 py-2 bg-white text-blue-700 hover:bg-blue-50 rounded-lg font-semibold text-sm">
              <FontAwesomeIcon icon={faPlus} /> Publier une offre
            </button>
            {isAdmin && (
              <button onClick={() => setOpenCreateCompany(true)}
                className="flex items-center gap-2 px-4 py-2 bg-white/15 hover:bg-white/25 text-white rounded-lg font-semibold text-sm">
                <FontAwesomeIcon icon={faBuilding} /> Gérer les entreprises
              </button>
            )}
          </div>
        )}
      </div>

      {openCreateOffer && <CreateOfferDialog onClose={() => setOpenCreateOffer(false)} />}

      {manageError && (
        <div className="p-3 bg-red-50 border-l-4 border-red-500 rounded text-sm text-red-700">
          <FontAwesomeIcon icon={faTriangleExclamation} className="mr-2" />{manageError}
        </div>
      )}
      {openCreateCompany && <CompaniesDialog onClose={() => setOpenCreateCompany(false)} />}
      {editingOffer && <CreateOfferDialog offer={editingOffer} onClose={() => setEditingOffer(null)} />}

      {/* Filters */}
      <div className="bg-white rounded-2xl p-5 border border-gray-200/80 shadow-sm flex flex-col gap-3">
        <div className="flex gap-3 flex-wrap items-end">
          <div className="flex-1" style={{ minWidth: '240px' }}>
            <label className="flex items-center gap-1.5 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faSearch} className="text-blue-500 text-xs" />
              <span>Recherche</span>
            </label>
            <div className="relative">
              <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-xs">
                <FontAwesomeIcon icon={faSearch} />
              </div>
              <input
                type="text"
                placeholder="Titre, technologies (React, .NET, IA...)"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full pl-9 pr-4 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
              />
            </div>
          </div>

          <div style={{ minWidth: '160px' }}>
            <label className="flex items-center gap-1.5 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faLocationDot} className="text-blue-500 text-xs" />
              <span>Ville</span>
            </label>
            <div className="relative">
              <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-xs">
                <FontAwesomeIcon icon={faLocationDot} />
              </div>
              <input
                type="text"
                placeholder="Ex: Casablanca..."
                value={city}
                onChange={(e) => setCity(e.target.value)}
                className="w-full pl-9 pr-4 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
              />
            </div>
          </div>

          <div style={{ minWidth: '170px' }}>
            <label className="flex items-center gap-1.5 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faGraduationCap} className="text-blue-500 text-xs" />
              <span>Filière</span>
            </label>
            <div className="relative">
              <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-xs">
                <FontAwesomeIcon icon={faGraduationCap} />
              </div>
              <select
                value={trackFilter}
                onChange={(e) => setTrackFilter(e.target.value)}
                className="w-full pl-9 pr-8 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm cursor-pointer box-border"
              >
                <option value="">Toutes les filières</option>
                {uniqueTracks.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
          </div>

          {hasFilters && (
            <button
              onClick={() => { setSearch(''); setCity(''); setTrackFilter(''); }}
              className="flex items-center justify-center gap-2 px-4 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl text-sm font-semibold transition-all shadow-sm active:scale-95 h-[42px]"
            >
              <FontAwesomeIcon icon={faRotateLeft} />
              Réinitialiser
            </button>
          )}
        </div>

        {hasFilters && (
          <div className="flex items-center justify-between pt-1 border-t border-gray-100 text-xs text-gray-500">
            <span>Résultats filtrés :</span>
            <span className="font-semibold text-blue-700 bg-blue-50 border border-blue-100 px-2.5 py-0.5 rounded-full">
              {filtered.length} / {allOffers.length} offre(s)
            </span>
          </div>
        )}
      </div>

      {/* Offers Grid */}
      {isLoading ? (
        <div className="text-center py-16 text-gray-500">
          <FontAwesomeIcon icon={faClock} className="text-4xl mb-3 text-blue-300" />
          <div>Chargement des offres...</div>
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16 bg-gray-50 rounded-2xl border-2 border-dashed border-gray-300">
          <FontAwesomeIcon icon={faMagnifyingGlass} className="text-5xl text-gray-300 mb-3" />
          <Title level="H4">Aucune offre trouvée</Title>
          <Text style={{ color: '#6b7280' }}>Modifiez vos critères de recherche.</Text>
        </div>
      ) : (
        <div className="grid gap-5" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))' }}>
          {filtered.map((offer) => {
            const trackColor = getTrackColor(offer.academicTrackCode);
            const skills = (offer.requiredSkills || '').split(',').map(s => s.trim()).filter(Boolean);
            return (
              <div key={offer.id}
                className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden flex flex-col transition-all hover:-translate-y-0.5 hover:shadow-card"
              >
                {/* Color banner */}
                <div style={{ background: `linear-gradient(135deg, ${trackColor} 0%, ${trackColor}cc 100%)`, padding: '16px 20px' }} className="text-white">
                  <div className="flex justify-between items-start">
                    <div className="text-lg font-bold leading-snug flex-1">{offer.title}</div>
                    <span className="ml-2 bg-white/25 rounded-xl px-2.5 py-0.5 text-xs font-bold whitespace-nowrap">
                      {offer.academicTrackCode}
                    </span>
                  </div>
                  <div className="mt-1.5 opacity-90 text-sm flex items-center gap-3">
                    <span><FontAwesomeIcon icon={faBuilding} className="mr-1" />{offer.companyName}</span>
                    <span><FontAwesomeIcon icon={faLocationDot} className="mr-1" />{offer.city}</span>
                  </div>
                </div>

                {/* Body */}
                <div className="p-5 flex-1 flex flex-col gap-3">
                  <p className="text-xs text-gray-600 leading-relaxed m-0">
                    {(offer.description || '').length > 130 ? offer.description.substring(0, 130) + '…' : offer.description}
                  </p>
                  {skills.length > 0 && (
                    <div className="flex flex-wrap gap-1.5">
                      {skills.slice(0, 5).map(s => (
                        <span key={s} style={{
                          background: `${trackColor}12`, color: trackColor,
                          border: `1px solid ${trackColor}30`,
                        }} className="rounded-xl px-2.5 py-0.5 text-xs font-semibold">
                          {s}
                        </span>
                      ))}
                    </div>
                  )}
                </div>

                {/* Footer */}
                <div className="border-t border-gray-100 px-5 py-3 flex justify-between items-center bg-gray-50">
                  <div className="flex items-center gap-3 text-xs text-gray-500">
                    <span><FontAwesomeIcon icon={faClock} className="mr-1" />{offer.durationInWeeks} sem.</span>
                    {offer.monthlyStipend ? (
                      <span className="text-green-600 font-semibold">
                        <FontAwesomeIcon icon={faCoins} className="mr-1" />{offer.monthlyStipend} DH/mois
                      </span>
                    ) : (
                      <span><FontAwesomeIcon icon={faCoins} className="mr-1" />Non précisée</span>
                    )}
                  </div>
                  {user?.role === 'Etudiant' && user.academicTrackId && user.academicTrackId !== offer.academicTrackId ? (
                    <span className="px-2.5 py-1 bg-gray-100 text-gray-500 rounded-xl text-xs font-semibold" title="Vous ne pouvez postuler qu'aux offres de votre filière">
                      Autre filière
                    </span>
                  ) : user?.role === 'Etudiant' ? (
                    <button
                      onClick={() => handleOpenApply(offer)}
                      className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-semibold transition-colors"
                    >
                      Postuler <FontAwesomeIcon icon={faArrowRight} />
                    </button>
                  ) : canManage(offer) ? (
                    <div className="flex items-center gap-1.5">
                      {offerState(offer)}
                      <button onClick={() => setEditingOffer(offer)}
                        className="px-2 py-1 border border-gray-300 hover:bg-gray-50 rounded-lg text-xs font-semibold text-gray-700">
                        Modifier
                      </button>
                      <button onClick={() => toggleActiveMutation.mutate({ id: offer.id, isActive: !offer.isActive })}
                        disabled={toggleActiveMutation.isPending}
                        className="px-2 py-1 border border-gray-300 hover:bg-gray-50 rounded-lg text-xs font-semibold text-gray-700">
                        {offer.isActive ? 'Désactiver' : 'Réactiver'}
                      </button>
                    </div>
                  ) : (
                    offerState(offer)
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Apply Dialog */}
      {selectedOffer && (
        <Dialog open headerText={`Postuler : ${selectedOffer.title}`}
          footer={
            <div className="flex justify-end gap-2 p-2">
              <Button onClick={() => setSelectedOffer(null)}>Annuler</Button>
              <Button design="Emphasized" onClick={() => applyMutation.mutate()} disabled={applyMutation.isPending}>
                {applyMutation.isPending ? 'Envoi…' : 'Créer le dossier de stage'}
              </Button>
            </div>
          }
        >
          <div className="p-4 flex flex-col gap-3" style={{ minWidth: '480px' }}>
            {/* Steps */}
            <div className="flex items-center gap-0 mb-1">
              {['1. Votre dossier', "2. Proposé à l'école", '3. Validation & démarrage'].map((step, idx) => (
                <React.Fragment key={step}>
                  <div className={`flex-1 text-center py-1.5 px-2 text-xs rounded-lg ${idx === 0 ? 'bg-blue-50 text-blue-700 font-bold border border-blue-200' : 'text-gray-400'}`}>
                    {step}
                  </div>
                  {idx < 2 && <FontAwesomeIcon icon={faArrowRight} className="text-gray-300 mx-1 text-sm" />}
                </React.Fragment>
              ))}
            </div>

            {applyError && (
              <div className="p-2.5 bg-red-50 text-red-600 rounded-lg text-sm">
                <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {applyError}
              </div>
            )}

            <div>
              <label className="block font-semibold text-xs mb-1">Titre du dossier :</label>
              <Input value={postulerTitle} onInput={(e: any) => setPostulerTitle(e.target.value)} style={{ width: '100%' }} />
            </div>
            <div>
              <label className="block font-semibold text-xs mb-1">Description du projet :</label>
              <TextArea value={postulerDesc} onInput={(e: any) => setPostulerDesc(e.target.value)} rows={3} style={{ width: '100%' }} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block font-semibold text-xs mb-1">Tuteur entreprise :</label>
                <Input value={supervisorName} onInput={(e: any) => setSupervisorName(e.target.value)} style={{ width: '100%' }} />
              </div>
              <div>
                <label className="block font-semibold text-xs mb-1">E-mail tuteur :</label>
                <Input value={supervisorEmail} onInput={(e: any) => setSupervisorEmail(e.target.value)} style={{ width: '100%' }} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block font-semibold text-xs mb-1">Date de début :</label>
                <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} style={inputStyle} />
              </div>
              <div>
                <label className="block font-semibold text-xs mb-1">Date de fin :</label>
                <input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} style={inputStyle} />
              </div>
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
};
export default OffersPage;
