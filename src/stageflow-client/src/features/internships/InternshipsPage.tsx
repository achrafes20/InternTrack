import React, { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Card, Title, Text, Button, Tag, Dialog, TextArea, Input } from '@ui5/webcomponents-react';
import { useNavigate } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faFolderOpen, faCheck, faTimes, faFilePdf, faPlus, faBuilding,
  faSearch, faFilter, faRotateLeft, faPaperPlane,
  faPlay, faUser, faCalendar, faIdCard, faBriefcase, faTriangleExclamation,
  faHourglass, faPenToSquare, faSignature, faChalkboardUser, faFileContract
} from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { useAuth } from '../../context/AuthContext';
import { Internship, CompanyRef, Track, Teacher, ConventionParty, apiErrorMessage } from '../../types';

const WORKFLOW_STEPS = [
  { key: 'Brouillon', label: 'Brouillon' },
  { key: 'Propose', label: 'Proposé' },
  { key: 'Valide', label: 'Validé' },
  { key: 'EnCours', label: 'En cours' },
  { key: 'SoutenancePlanifiee', label: 'Soutenance' },
  { key: 'Termine', label: 'Terminé' },
];

const getStatusOrder = (statusStr: string): number => {
  const idx = WORKFLOW_STEPS.findIndex(s => s.key === statusStr);
  return idx === -1 ? 0 : idx;
};

const WorkflowStepper: React.FC<{ currentStatus: string }> = ({ currentStatus }) => {
  const currentIdx = getStatusOrder(currentStatus);
  const isRefused = currentStatus === 'Refuse';

  return (
    <div className="flex items-center mb-4 flex-wrap gap-0">
      {WORKFLOW_STEPS.map((step, idx) => {
        const isCompleted = idx < currentIdx;
        const isCurrent = idx === currentIdx && !isRefused;

        const circleClass = isRefused && idx === 0
          ? 'bg-red-500'
          : isCompleted
          ? 'bg-green-600'
          : isCurrent
          ? 'bg-blue-600 ring-4 ring-blue-100'
          : 'bg-gray-300';

        const textClass = isCompleted ? 'text-green-700' : isCurrent ? 'text-blue-700 font-bold' : 'text-gray-400';

        return (
          <React.Fragment key={step.key}>
            <div className="flex flex-col items-center" style={{ minWidth: '56px' }}>
              <div className={`w-7 h-7 rounded-full text-white flex items-center justify-center text-xs font-bold transition-all ${circleClass}`}>
                {isCompleted ? <FontAwesomeIcon icon={faCheck} /> : idx + 1}
              </div>
              <span className={`text-[10px] mt-1 text-center ${textClass}`}>{step.label}</span>
            </div>
            {idx < WORKFLOW_STEPS.length - 1 && (
              <div className={`flex-1 h-0.5 mb-3.5 ${isCompleted ? 'bg-green-500' : 'bg-gray-200'}`} style={{ minWidth: '10px' }} />
            )}
          </React.Fragment>
        );
      })}
      {isRefused && (
        <span className="ml-2 mb-3.5 px-2 py-0.5 bg-red-100 text-red-700 rounded-full text-xs font-bold">Refusé</span>
      )}
    </div>
  );
};

const STATUS_CONFIG: Record<string, { label: string; colorScheme: string; className: string }> = {
  Brouillon: { label: 'Brouillon', colorScheme: '9', className: 'bg-gray-100 text-gray-700' },
  Propose: { label: 'Proposé', colorScheme: '6', className: 'bg-amber-100 text-amber-800' },
  Valide: { label: 'Validé', colorScheme: '5', className: 'bg-blue-100 text-blue-800' },
  Refuse: { label: 'Refusé', colorScheme: '2', className: 'bg-red-100 text-red-700' },
  EnCours: { label: 'En cours', colorScheme: '7', className: 'bg-green-100 text-green-800' },
  SoutenancePlanifiee: { label: 'Soutenance planifiée', colorScheme: '8', className: 'bg-indigo-100 text-indigo-800' },
  Termine: { label: 'Terminé', colorScheme: '10', className: 'bg-slate-100 text-slate-700' },
};

const STATUS_BAR_COLOR: Record<string, string> = {
  EnCours: '#107e3e', Valide: '#0070f3', Refuse: '#e53935',
  Termine: '#6b7280', Propose: '#f5a623', SoutenancePlanifiee: '#6366f1',
};

export const InternshipsPage: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const isStudent = user?.role === 'Etudiant';
  const isCoord = user?.role === 'ResponsableFiliere' || user?.role === 'Admin';

  const [rejectId, setRejectId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [openProposeModal, setOpenProposeModal] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [approveId, setApproveId] = useState<string | null>(null);
  const [actionError, setActionError] = useState('');
  const [assignTarget, setAssignTarget] = useState<Internship | null>(null);
  const [assignSupervisorId, setAssignSupervisorId] = useState('');

  const [searchText, setSearchText] = useState('');
  const [statusFilter, setStatusFilter] = useState('Tous');

  const [title, setTitle] = useState('');
  const [subjectDescription, setSubjectDescription] = useState('');
  const [companyId, setCompanyId] = useState('');
  const [trackId, setTrackId] = useState('');
  const [supervisorName, setSupervisorName] = useState('');
  const [supervisorEmail, setSupervisorEmail] = useState('');
  const [supervisorPhone, setSupervisorPhone] = useState('');
  const [startDate, setStartDate] = useState('2026-03-01');
  const [endDate, setEndDate] = useState('2026-08-31');
  const [proposeError, setProposeError] = useState('');

  const { data: internships, isLoading } = useQuery<Internship[]>({
    queryKey: ['internships'],
    queryFn: async () => { const res = await api.get('/internships'); return res.data; },
  });

  const { data: companies = [] } = useQuery<CompanyRef[]>({
    queryKey: ['companies'],
    queryFn: async () => { const res = await api.get('/companies'); return res.data; },
    enabled: openProposeModal,
  });

  const { data: tracks = [] } = useQuery<Track[]>({
    queryKey: ['tracks'],
    queryFn: async () => { const res = await api.get('/tracks'); return res.data; },
    enabled: openProposeModal,
  });

  const { data: teachers = [] } = useQuery<Teacher[]>({
    queryKey: ['teachers'],
    queryFn: async () => { const res = await api.get('/teachers'); return res.data; },
    enabled: isCoord && !!assignTarget,
  });

  /** Mutation standard : rafraîchit la liste et affiche l'erreur métier renvoyée par l'API. */
  const useAction = <T,>(fn: (arg: T) => Promise<unknown>, onDone?: () => void) => useMutation({
    mutationFn: async (arg: T) => { setActionError(''); await fn(arg); },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['internships'] }); onDone?.(); },
    onError: (err: any) => setActionError(apiErrorMessage(err, "L'action a échoué.")),
  });

  const submitMutation = useAction((id: string) => api.post(`/internships/${id}/submit`));
  const reopenMutation = useAction((id: string) => api.post(`/internships/${id}/reopen`));
  const startMutation = useAction((id: string) => api.post(`/internships/${id}/start`));
  const approveMutation = useAction((id: string) => api.post(`/internships/${id}/approve`), () => setApproveId(null));
  const rejectMutation = useAction(
    ({ id, reason }: { id: string; reason: string }) => api.post(`/internships/${id}/reject`, { rejectionReason: reason }),
    () => { setRejectId(null); setRejectReason(''); });
  const signMutation = useAction(
    ({ id, party }: { id: string; party: ConventionParty }) => api.post(`/conventions/internship/${id}/sign`, { party }));
  const assignMutation = useAction(
    ({ id, supervisorId }: { id: string; supervisorId: string }) => api.post(`/internships/${id}/assign-supervisor`, { supervisorId }),
    () => { setAssignTarget(null); setAssignSupervisorId(''); });

  const proposeMutation = useMutation({
    mutationFn: async () => {
      setProposeError('');
      const selectedCompId = companyId || (companies.length > 0 ? companies[0].id : '');
      if (!selectedCompId) throw new Error('Veuillez sélectionner une entreprise.');
      const payload = {
        title, subjectDescription,
        companyId: selectedCompId,
        companySupervisorName: supervisorName, companySupervisorEmail: supervisorEmail,
        companySupervisorPhone: supervisorPhone || null,
        startDate: new Date(startDate).toISOString(), endDate: new Date(endDate).toISOString(),
      };

      if (editId) {
        await api.put(`/internships/${editId}`, payload);
      } else {
        const selectedTrackId = user?.academicTrackId || trackId || (tracks.length > 0 ? tracks[0].id : '');
        if (!selectedTrackId) throw new Error('Votre compte doit être rattaché à une filière.');
        await api.post('/internships', { ...payload, academicTrackId: selectedTrackId });
      }
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['internships'] }); setOpenProposeModal(false); resetProposeForm(); },
    onError: (err: any) => { setProposeError(apiErrorMessage(err, "Erreur lors de l'enregistrement du dossier.")); },
  });

  const resetProposeForm = () => {
    setEditId(null);
    setTitle(''); setSubjectDescription(''); setCompanyId('');
    setSupervisorName(''); setSupervisorEmail(''); setSupervisorPhone(''); setProposeError('');
  };

  const openCreateModal = () => { resetProposeForm(); setOpenProposeModal(true); };

  const openEditModal = (i: Internship) => {
    resetProposeForm();
    setEditId(i.id);
    setTitle(i.title); setSubjectDescription(i.subjectDescription); setCompanyId(i.companyId);
    setSupervisorName(i.companySupervisorName); setSupervisorEmail(i.companySupervisorEmail);
    setSupervisorPhone(i.companySupervisorPhone || '');
    setStartDate(i.startDate.substring(0, 10)); setEndDate(i.endDate.substring(0, 10));
    setOpenProposeModal(true);
  };

  const getStatusString = (i: Internship): string => i.statusName || i.status;

  const handleDownloadPdf = async (id: string) => {
    try {
      setActionError('');
      const response = await api.get(`/conventions/internship/${id}/pdf`, { responseType: 'blob' });
      const blob = new Blob([response.data], { type: 'application/pdf' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.setAttribute('download', `convention_${id}.pdf`);
      document.body.appendChild(link); link.click(); link.remove();
      window.URL.revokeObjectURL(url);
    } catch { setActionError("La convention de stage n'est pas encore disponible."); }
  };

  const filteredInternships = useMemo(() => {
    if (!internships) return [];
    return internships.filter((i) => {
      const st = getStatusString(i);
      const matchesSearch = !searchText ||
        (i.title || '').toLowerCase().includes(searchText.toLowerCase()) ||
        (i.companyName || '').toLowerCase().includes(searchText.toLowerCase());
      const matchesStatus = statusFilter === 'Tous' || st === statusFilter;
      return matchesSearch && matchesStatus;
    });
  }, [internships, searchText, statusFilter]);

  const statusOptions = [
    { value: 'Tous', label: 'Tous les statuts' }, { value: 'Brouillon', label: 'Brouillon' },
    { value: 'Propose', label: 'Proposé' }, { value: 'Valide', label: 'Validé' },
    { value: 'Refuse', label: 'Refusé' },
    { value: 'EnCours', label: 'En cours' }, { value: 'SoutenancePlanifiee', label: 'Soutenance planifiée' },
    { value: 'Termine', label: 'Terminé' },
  ];

  const showStudentName = user?.role === 'ResponsableFiliere' || user?.role === 'Admin' || user?.role === 'Encadrant';

  const inputSelectStyle = {
    width: '100%', padding: '8px 12px', borderRadius: '6px',
    border: '1px solid #d1d5db', fontSize: '14px', boxSizing: 'border-box' as const
  };

  return (
    <div className="flex flex-col gap-5">
      {/* Header */}
      <div className="flex justify-between items-center flex-wrap gap-3">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <FontAwesomeIcon icon={faFolderOpen} className="text-blue-600 text-xl" />
            <Title level="H2">Dossiers de Stage &amp; PFE</Title>
          </div>
          <Text style={{ color: '#555', display: 'block' }}>
            Suivi du cycle de vie des stages selon le workflow officiel ENSA.
          </Text>
        </div>
        {user?.role === 'Etudiant' && (
          <button
            onClick={openCreateModal}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-semibold transition-colors text-sm"
          >
            <FontAwesomeIcon icon={faPlus} />
            Proposer un stage spontané
          </button>
        )}
      </div>

      {/* Filter Bar */}
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
                placeholder="Titre du stage ou nom d'entreprise..."
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
                className="w-full pl-9 pr-4 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm box-border"
              />
            </div>
          </div>

          <div style={{ minWidth: '200px' }}>
            <label className="flex items-center gap-1.5 text-xs font-semibold text-gray-600 mb-1.5">
              <FontAwesomeIcon icon={faFilter} className="text-blue-500 text-xs" />
              <span>Statut du workflow</span>
            </label>
            <div className="relative">
              <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-xs">
                <FontAwesomeIcon icon={faFilter} />
              </div>
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="w-full pl-9 pr-8 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm cursor-pointer box-border"
              >
                {statusOptions.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
          </div>

          {(searchText || statusFilter !== 'Tous') && (
            <button
              onClick={() => { setSearchText(''); setStatusFilter('Tous'); }}
              className="flex items-center justify-center gap-2 px-4 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl text-sm font-semibold transition-all shadow-sm active:scale-95 h-[42px]"
            >
              <FontAwesomeIcon icon={faRotateLeft} />
              Réinitialiser
            </button>
          )}

          {internships && (
            <div className="ml-auto self-center">
              <span className="text-xs font-semibold px-3 py-1.5 rounded-full bg-blue-50 text-blue-700 border border-blue-100">
                {filteredInternships.length} / {internships.length} dossier(s)
              </span>
            </div>
          )}
        </div>
      </div>

      {actionError && (
        <div className="p-3 bg-red-50 border-l-4 border-red-500 rounded text-sm text-red-700 flex justify-between items-center">
          <span><FontAwesomeIcon icon={faTriangleExclamation} className="mr-2" />{actionError}</span>
          <button onClick={() => setActionError('')} className="text-red-500 hover:text-red-700" aria-label="Fermer">
            <FontAwesomeIcon icon={faTimes} />
          </button>
        </div>
      )}

      {/* Content */}
      {isLoading ? (
        <Card style={{ padding: '40px', textAlign: 'center' }}>
          <FontAwesomeIcon icon={faHourglass} className="text-4xl text-blue-400 mb-3" />
          <Text style={{ color: '#666' }}>Chargement des dossiers...</Text>
        </Card>
      ) : filteredInternships.length === 0 ? (
        <Card style={{ padding: '48px', textAlign: 'center' }}>
          <FontAwesomeIcon icon={faFolderOpen} className="text-6xl text-gray-300 mb-4" />
          <Title level="H4" style={{ marginBottom: '8px' }}>Aucun dossier de stage</Title>
          <Text style={{ display: 'block', margin: '0 0 20px', color: '#666' }}>
            {searchText || statusFilter !== 'Tous'
              ? "Aucun résultat pour vos filtres. Essayez d'élargir votre recherche."
              : "Vous n'avez pas encore de dossier de stage actif ou proposé."}
          </Text>
          <div className="flex justify-center gap-3 flex-wrap">
            <Button design="Emphasized" onClick={() => navigate('/offers')}>
              Consulter les offres disponibles
            </Button>
            {user?.role === 'Etudiant' && (
              <Button onClick={openCreateModal}>Proposer un stage spontané</Button>
            )}
          </div>
        </Card>
      ) : (
        <div className="flex flex-col gap-4">
          {filteredInternships.map((i) => {
            const st = getStatusString(i);
            const isPropose = st === 'Propose';
            const isValide = st === 'Valide';
            const isBrouillon = st === 'Brouillon';
            const isRefuse = st === 'Refuse';
            const isActiveFile = ['Valide', 'EnCours', 'SoutenancePlanifiee'].includes(st);
            const conv = i.convention;
            const isPdfReady = !!conv;
            const statusCfg = STATUS_CONFIG[st] || { label: st, colorScheme: '9', className: 'bg-gray-100 text-gray-700' };
            const barColor = STATUS_BAR_COLOR[st] || '#bdbdbd';

            const startDateFormatted = i.startDate
              ? new Date(i.startDate).toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '—';
            const endDateFormatted = i.endDate
              ? new Date(i.endDate).toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '—';

            return (
              <div key={i.id} className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                <div style={{ height: '4px', background: barColor }} />
                <div className="p-5">
                  <WorkflowStepper currentStatus={st} />

                  <div className="flex justify-between items-start flex-wrap gap-4">
                    <div className="flex-1" style={{ minWidth: '280px' }}>
                      <div className="flex items-center gap-2 flex-wrap mb-2">
                        <span className="text-base font-bold text-gray-900">{i.title}</span>
                        <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${statusCfg.className}`}>
                          {statusCfg.label}
                        </span>
                      </div>

                      <div className="flex items-center gap-3 flex-wrap mb-2">
                        <span className="text-blue-600 font-semibold flex items-center gap-1 text-sm">
                          <FontAwesomeIcon icon={faBuilding} />
                          {i.companyName}
                        </span>
                        {i.academicTrackCode && (
                          <Tag colorScheme="8">{i.academicTrackCode}</Tag>
                        )}
                      </div>

                      <div className="flex items-center gap-1 text-xs text-gray-500 mb-1.5">
                        <FontAwesomeIcon icon={faCalendar} />
                        Du <strong className="mx-0.5">{startDateFormatted}</strong> au <strong className="mx-0.5">{endDateFormatted}</strong>
                      </div>

                      {i.companySupervisorName && (
                        <div className="flex items-center gap-1 text-xs text-gray-500 mb-1.5">
                          <FontAwesomeIcon icon={faIdCard} />
                          Tuteur : {i.companySupervisorName}
                          {i.companySupervisorEmail && ` · ${i.companySupervisorEmail}`}
                        </div>
                      )}

                      {showStudentName && i.studentName && (
                        <div className="flex items-center gap-1 text-xs text-gray-600 mb-1.5">
                          <FontAwesomeIcon icon={faUser} />
                          <strong>{i.studentName}</strong>
                        </div>
                      )}

                      <div className="flex items-center gap-1 text-xs text-gray-500 mb-1.5">
                        <FontAwesomeIcon icon={faChalkboardUser} />
                        Encadrant pédagogique : {i.academicSupervisorName
                          ? <strong className="ml-0.5">{i.academicSupervisorName}</strong>
                          : <em className="ml-0.5 text-amber-700">non affecté</em>}
                      </div>

                      {conv && (
                        <div className="mt-2 p-2.5 bg-slate-50 border border-slate-200 rounded text-xs text-slate-700">
                          <div className="flex items-center gap-1 font-semibold mb-1">
                            <FontAwesomeIcon icon={faFileContract} /> Convention {conv.conventionNumber}
                            {conv.signedAt
                              ? <span className="ml-1 text-green-700">— signée par les trois parties</span>
                              : <span className="ml-1 text-amber-700">— en attente de signatures</span>}
                          </div>
                          <div className="flex gap-3 flex-wrap">
                            {([['Élève', conv.isSignedByStudent], ['Entreprise', conv.isSignedByCompany], ['École', conv.isSignedBySchool]] as const).map(([label, ok]) => (
                              <span key={label} className={ok ? 'text-green-700' : 'text-gray-400'}>
                                <FontAwesomeIcon icon={ok ? faCheck : faHourglass} className="mr-1" />{label}
                              </span>
                            ))}
                          </div>
                        </div>
                      )}

                      {i.subjectDescription && (
                        <p className="mt-2 text-gray-500 text-xs leading-relaxed">
                          {i.subjectDescription.length > 150
                            ? i.subjectDescription.substring(0, 150) + '...'
                            : i.subjectDescription}
                        </p>
                      )}

                      {i.rejectionReason && (
                        <div className="mt-2 p-2.5 bg-red-50 border-l-2 border-red-500 rounded text-xs text-red-700">
                          <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" />
                          <strong>Motif de refus :</strong> {i.rejectionReason}
                        </div>
                      )}
                    </div>

                    {/* Actions */}
                    <div className="flex flex-col gap-2 items-stretch" style={{ minWidth: '180px' }}>
                      {isPdfReady && (
                        <button
                          onClick={() => handleDownloadPdf(i.id)}
                          className="flex items-center justify-center gap-2 px-3 py-2 border border-gray-300 rounded-lg text-xs text-gray-700 hover:bg-gray-50 transition-colors"
                        >
                          <FontAwesomeIcon icon={faFilePdf} className="text-red-500" />
                          Télécharger la convention PDF
                        </button>
                      )}
                      {isStudent && isBrouillon && (
                        <>
                          <Button onClick={() => openEditModal(i)}>
                            <FontAwesomeIcon icon={faPenToSquare} className="mr-1" /> Modifier
                          </Button>
                          <Button design="Emphasized" onClick={() => submitMutation.mutate(i.id)} disabled={submitMutation.isPending}>
                            <FontAwesomeIcon icon={faPaperPlane} className="mr-1" /> Soumettre
                          </Button>
                        </>
                      )}
                      {isStudent && isRefuse && (
                        <Button onClick={() => reopenMutation.mutate(i.id)} disabled={reopenMutation.isPending}>
                          <FontAwesomeIcon icon={faRotateLeft} className="mr-1" /> Retravailler le dossier
                        </Button>
                      )}
                      {isStudent && conv && !conv.isSignedByStudent && isActiveFile && (
                        <Button design="Emphasized" onClick={() => signMutation.mutate({ id: i.id, party: 'Student' })} disabled={signMutation.isPending}>
                          <FontAwesomeIcon icon={faSignature} className="mr-1" /> Signer la convention
                        </Button>
                      )}
                      {isCoord && conv && !conv.isSignedBySchool && isActiveFile && (
                        <Button onClick={() => signMutation.mutate({ id: i.id, party: 'School' })} disabled={signMutation.isPending}>
                          <FontAwesomeIcon icon={faSignature} className="mr-1" /> Signer pour l'école
                        </Button>
                      )}
                      {isCoord && conv && !conv.isSignedByCompany && isActiveFile && (
                        <Button onClick={() => signMutation.mutate({ id: i.id, party: 'Company' })} disabled={signMutation.isPending}
                          tooltip="À utiliser à réception de l'exemplaire signé par l'entreprise">
                          <FontAwesomeIcon icon={faBuilding} className="mr-1" /> Enregistrer la signature entreprise
                        </Button>
                      )}
                      {isCoord && !['Refuse', 'Termine'].includes(st) && (
                        <Button onClick={() => { setAssignTarget(i); setAssignSupervisorId(i.academicSupervisorId || ''); }}>
                          <FontAwesomeIcon icon={faChalkboardUser} className="mr-1" />
                          {i.academicSupervisorId ? "Changer d'encadrant" : 'Affecter un encadrant'}
                        </Button>
                      )}
                      {isCoord && isPropose && (
                        <>
                          <Button design="Positive" onClick={() => setApproveId(i.id)}>
                            <FontAwesomeIcon icon={faCheck} className="mr-1" /> Valider
                          </Button>
                          <Button design="Negative" onClick={() => setRejectId(i.id)}>
                            <FontAwesomeIcon icon={faTimes} className="mr-1" /> Refuser
                          </Button>
                        </>
                      )}
                      {isCoord && isValide && (
                        <Button design="Emphasized" onClick={() => startMutation.mutate(i.id)}>
                          <FontAwesomeIcon icon={faPlay} className="mr-1" /> Démarrer le stage
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Dialog: Confirm Approval */}
      {approveId && (
        <Dialog open headerText="Confirmer la validation"
          footer={
            <div className="flex justify-end gap-2 p-2">
              <Button onClick={() => setApproveId(null)}>Annuler</Button>
              <Button design="Positive" onClick={() => approveMutation.mutate(approveId)} disabled={approveMutation.isPending}>
                {approveMutation.isPending ? 'Validation...' : 'Confirmer la validation'}
              </Button>
            </div>
          }
        >
          <div className="p-5 text-center" style={{ minWidth: '360px' }}>
            <FontAwesomeIcon icon={faCheck} className="text-5xl text-green-500 mb-3" />
            <p className="text-sm text-gray-700">
              Êtes-vous sûr de vouloir <strong>valider</strong> ce dossier de stage ?
            </p>
            <p className="text-xs text-gray-500 mt-2">
              L'élève ingénieur sera notifié et pourra procéder à la signature de la convention.
            </p>
          </div>
        </Dialog>
      )}

      {/* Dialog: Reject */}
      {rejectId && (
        <Dialog open headerText="Motif de refus du stage"
          footer={
            <div className="flex justify-end gap-2 p-2">
              <Button onClick={() => setRejectId(null)}>Annuler</Button>
              <Button design="Negative" onClick={() => rejectMutation.mutate({ id: rejectId, reason: rejectReason })} disabled={rejectMutation.isPending}>
                {rejectMutation.isPending ? 'Refus en cours...' : 'Confirmer le refus'}
              </Button>
            </div>
          }
        >
          <div className="p-4" style={{ minWidth: '400px' }}>
            <Text style={{ display: 'block', marginBottom: '12px', color: '#444' }}>
              Veuillez préciser le motif pédagogique ou administratif de refus :
            </Text>
            <TextArea value={rejectReason} onInput={(e: any) => setRejectReason(e.target.value)} rows={4}
              style={{ width: '100%' }} placeholder="Ex: Sujet insuffisant pour un projet de fin d'études 5e année..." />
          </div>
        </Dialog>
      )}

      {/* Dialog: Proposer un stage spontané */}
      {openProposeModal && (
        <Dialog open headerText={editId ? 'Modifier le brouillon' : 'Proposer un stage spontané'}
          footer={
            <div className="flex justify-end gap-2 p-2">
              <Button onClick={() => { setOpenProposeModal(false); resetProposeForm(); }}>Annuler</Button>
              <Button design="Emphasized" onClick={() => proposeMutation.mutate()} disabled={proposeMutation.isPending}>
                {proposeMutation.isPending ? 'Enregistrement...' : editId ? 'Enregistrer les modifications' : 'Créer le dossier'}
              </Button>
            </div>
          }
        >
          <div className="p-4 flex flex-col gap-3" style={{ minWidth: '480px', maxWidth: '600px' }}>
            {proposeError && (
              <div className="p-2.5 bg-red-50 border-l-2 border-red-500 rounded text-sm text-red-700">
                <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {proposeError}
              </div>
            )}

            <div className="p-2.5 bg-blue-50 rounded text-sm text-blue-800 font-semibold">
              <FontAwesomeIcon icon={faBriefcase} className="mr-2" /> Section 1 — Informations du stage
            </div>
            <div>
              <Text style={{ fontWeight: 600, fontSize: '13px' }}>Titre du stage :</Text>
              <Input value={title} placeholder="Ex: Refonte de l'architecture microservices"
                onInput={(e: any) => setTitle(e.target.value)} style={{ width: '100%', marginTop: '4px' }} />
            </div>
            <div>
              <Text style={{ fontWeight: 600, fontSize: '13px' }}>Description détaillée :</Text>
              <TextArea value={subjectDescription} placeholder="Objectifs, technologies utilisées, livrables attendus..."
                onInput={(e: any) => setSubjectDescription(e.target.value)} rows={3} style={{ width: '100%', marginTop: '4px' }} />
            </div>

            <div className="p-2.5 bg-green-50 rounded text-sm text-green-800 font-semibold">
              <FontAwesomeIcon icon={faBuilding} className="mr-2" /> Section 2 — Entreprise d'accueil
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Text style={{ fontWeight: 600, fontSize: '13px' }}>Entreprise d'accueil :</Text>
                <select value={companyId || (companies[0]?.id || '')} onChange={(e) => setCompanyId(e.target.value)}
                  style={{ ...inputSelectStyle, marginTop: '4px' }}>
                  {companies.map((c) => <option key={c.id} value={c.id}>{c.name} ({c.city})</option>)}
                </select>
              </div>
              <div>
                <Text style={{ fontWeight: 600, fontSize: '13px' }}>Filière ENSA :</Text>
                {/* L'élève ne peut proposer un stage que dans sa propre filière (contrôlé aussi côté serveur). */}
                <select value={user?.academicTrackId || trackId || (tracks[0]?.id || '')} onChange={(e) => setTrackId(e.target.value)}
                  disabled={!!user?.academicTrackId || !!editId}
                  style={{ ...inputSelectStyle, marginTop: '4px' }}>
                  {tracks.map((t) => <option key={t.id} value={t.id}>{t.code} - {t.name}</option>)}
                </select>
              </div>
            </div>

            <div className="p-2.5 bg-pink-50 rounded text-sm text-pink-800 font-semibold">
              <FontAwesomeIcon icon={faIdCard} className="mr-2" /> Section 3 — Tuteur entreprise
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Text style={{ fontWeight: 600, fontSize: '13px' }}>Nom tuteur :</Text>
                <Input value={supervisorName} placeholder="Ex: Karim Idrissi"
                  onInput={(e: any) => setSupervisorName(e.target.value)} style={{ width: '100%', marginTop: '4px' }} />
              </div>
              <div>
                <Text style={{ fontWeight: 600, fontSize: '13px' }}>Email tuteur :</Text>
                <Input value={supervisorEmail} placeholder="k.idrissi@entreprise.ma"
                  onInput={(e: any) => setSupervisorEmail(e.target.value)} style={{ width: '100%', marginTop: '4px' }} />
              </div>
            </div>
            <div>
              <Text style={{ fontWeight: 600, fontSize: '13px' }}>Téléphone tuteur (Optionnel) :</Text>
              <Input value={supervisorPhone} placeholder="+212600112233"
                onInput={(e: any) => setSupervisorPhone(e.target.value)} style={{ width: '100%', marginTop: '4px' }} />
            </div>

            <div className="p-2.5 bg-amber-50 rounded text-sm text-amber-800 font-semibold">
              <FontAwesomeIcon icon={faCalendar} className="mr-2" /> Section 4 — Période du stage
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Text style={{ fontWeight: 600, fontSize: '13px' }}>Date début :</Text>
                <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)}
                  style={{ ...inputSelectStyle, marginTop: '4px' }} />
              </div>
              <div>
                <Text style={{ fontWeight: 600, fontSize: '13px' }}>Date fin :</Text>
                <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)}
                  style={{ ...inputSelectStyle, marginTop: '4px' }} />
              </div>
            </div>
          </div>
        </Dialog>
      )}

      {/* Dialog: Affecter un encadrant pédagogique */}
      {assignTarget && (
        <Dialog open headerText="Affecter un encadrant pédagogique"
          footer={
            <div className="flex justify-end gap-2 p-2">
              <Button onClick={() => setAssignTarget(null)}>Annuler</Button>
              <Button design="Emphasized" disabled={!assignSupervisorId || assignMutation.isPending}
                onClick={() => assignMutation.mutate({ id: assignTarget.id, supervisorId: assignSupervisorId })}>
                {assignMutation.isPending ? 'Affectation...' : 'Affecter'}
              </Button>
            </div>
          }
        >
          <div className="p-4 flex flex-col gap-3" style={{ minWidth: '400px' }}>
            <Text><strong>{assignTarget.title}</strong>{assignTarget.studentName ? ` — ${assignTarget.studentName}` : ''}</Text>
            <select value={assignSupervisorId} onChange={(e) => setAssignSupervisorId(e.target.value)} style={inputSelectStyle}>
              <option value="">-- Choisir un enseignant --</option>
              {teachers.map((t) => (
                <option key={t.id} value={t.id}>{t.fullName} ({t.role === 'ResponsableFiliere' ? 'Responsable' : 'Encadrant'})</option>
              ))}
            </select>
            <Text style={{ fontSize: '12px', color: '#666' }}>
              L'encadrant pourra consulter le dossier, relire et valider les rapports hebdomadaires.
            </Text>
          </div>
        </Dialog>
      )}
    </div>
  );
};

export default InternshipsPage;
