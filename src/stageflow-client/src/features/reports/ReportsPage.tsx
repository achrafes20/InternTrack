import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Card, Button, Tag, Dialog, Input, TextArea, Text,
  BusyIndicator, Bar, Title
} from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faFileLines, faComment, faRobot, faCheck, faPlus,
  faChevronDown, faChevronUp, faUser, faClipboardList,
  faTriangleExclamation, faCalendarDays, faBuildingColumns, faFolderOpen, faPenToSquare
} from '@fortawesome/free-solid-svg-icons';
import { apiClient } from '../../api/client';
import { ProgressReportDto, InternshipDto, apiErrorMessage } from '../../types';
import { useAuth } from '../../context/AuthContext';

export const ReportsPage: React.FC = () => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const isStudent = user?.role === 'Etudiant';
  const isSupervisorOrHead = user?.role === 'Encadrant' || user?.role === 'ResponsableFiliere' || user?.role === 'Admin';

  const [openSubmitDialog, setOpenSubmitDialog] = useState(false);
  const [openCommentDialog, setOpenCommentDialog] = useState(false);
  const [openAiDialog, setOpenAiDialog] = useState(false);
  const [expandedCards, setExpandedCards] = useState<Record<string, boolean>>({});

  const [selectedReport, setSelectedReport] = useState<ProgressReportDto | null>(null);
  const [selectedInternshipId, setSelectedInternshipId] = useState<string>('');
  const [weekNumber, setWeekNumber] = useState(1);
  const [reportTitle, setReportTitle] = useState('');
  const [activities, setActivities] = useState('');
  const [skills, setSkills] = useState('');
  const [nextGoals, setNextGoals] = useState('');
  const [difficulties, setDifficulties] = useState('');
  const [commentText, setCommentText] = useState('');
  const [aiSummary, setAiSummary] = useState('');
  const [aiLoading, setAiLoading] = useState(false);
  const [submitError, setSubmitError] = useState('');

  const { data: myInternships = [] } = useQuery<InternshipDto[]>({
    queryKey: ['my-internships'],
    queryFn: async () => { const res = await apiClient.get('/internships'); return res.data as InternshipDto[]; },
    enabled: isStudent
  });

  const activeInternship = myInternships.find(i => i.id === selectedInternshipId) || myInternships[0];

  const { data: reports = [], isLoading } = useQuery<ProgressReportDto[]>({
    queryKey: ['reports', isStudent ? activeInternship?.id : 'all'],
    queryFn: async () => {
      if (isStudent && activeInternship) {
        const res = await apiClient.get(`/reports/internship/${activeInternship.id}`);
        return res.data;
      } else if (isSupervisorOrHead) {
        const res = await apiClient.get('/reports');
        return res.data;
      }
      return [];
    },
    enabled: !!activeInternship || isSupervisorOrHead
  });

  const [editingReportId, setEditingReportId] = useState<string | null>(null);

  const submitReportMutation = useMutation({
    mutationFn: async () => {
      if (!activeInternship) return;
      setSubmitError('');
      const content = {
        title: reportTitle, activitiesPerformed: activities, skillsAcquired: skills,
        difficultiesEncountered: difficulties, nextWeekGoals: nextGoals
      };
      if (editingReportId) {
        await apiClient.put(`/reports/${editingReportId}`, content);
      } else {
        await apiClient.post('/reports', { internshipId: activeInternship.id, weekNumber: Number(weekNumber), ...content });
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reports'] });
      closeSubmitDialog();
    },
    onError: (err: any) => { setSubmitError(apiErrorMessage(err, 'Erreur lors du dépôt du rapport.')); }
  });

  const closeSubmitDialog = () => {
    setOpenSubmitDialog(false); setEditingReportId(null); setReportTitle(''); setActivities(''); setSkills('');
    setNextGoals(''); setDifficulties(''); setSubmitError('');
  };

  const openEditDialog = (r: ProgressReportDto) => {
    setEditingReportId(r.id);
    setWeekNumber(r.weekNumber);
    setReportTitle(r.title); setActivities(r.activitiesPerformed); setSkills(r.skillsAcquired);
    setDifficulties(r.difficultiesEncountered); setNextGoals(r.nextWeekGoals); setSubmitError('');
    setOpenSubmitDialog(true);
  };

  const validateReportMutation = useMutation({
    mutationFn: async (reportId: string) => { await apiClient.post(`/reports/${reportId}/validate`); },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['reports'] }); }
  });

  const addCommentMutation = useMutation({
    mutationFn: async () => {
      if (!selectedReport) return;
      await apiClient.post(`/reports/${selectedReport.id}/comments`, { content: commentText });
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['reports'] }); setOpenCommentDialog(false); setCommentText(''); }
  });

  const handleSummarizeWithAi = async (report: ProgressReportDto) => {
    setSelectedReport(report); setAiLoading(true); setAiSummary(''); setOpenAiDialog(true);
    try {
      const res = await apiClient.post(`/reports/${report.id}/summarize`);
      setAiSummary(res.data.summary);
    } catch (e: any) {
      setAiSummary('Échec de la génération du résumé : ' + (e.response?.data?.detail || e.message));
    } finally { setAiLoading(false); }
  };

  const toggleCard = (id: string) => setExpandedCards(prev => ({ ...prev, [id]: !prev[id] }));

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '';
    return new Date(dateStr).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
  };

  const truncate = (text: string, max: number) => text && text.length > max ? text.substring(0, max) + '...' : text || '';

  const selectStyle = {
    padding: '0.5rem', borderRadius: '4px', border: '1px solid #ccc', flex: 1, fontSize: '14px'
  };

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="bg-gradient-to-r from-ensa-navy to-ensa-blue rounded-2xl p-6 text-white">
        <div className="flex justify-between items-center flex-wrap gap-4">
          <div>
            <div className="flex items-center gap-3 mb-1">
              <FontAwesomeIcon icon={faFileLines} className="text-2xl opacity-90" />
              <div className="text-xl font-bold">Rapports de Suivi Hebdomadaire</div>
            </div>
            <div className="text-sm opacity-85">
              Dépôt des rapports d'avancement, validation pédagogique par l'encadrant et résumé intelligent IA.
            </div>
            {isStudent && activeInternship && (
              <div className="mt-3 inline-block bg-white/15 rounded-lg px-4 py-2 text-sm">
                <FontAwesomeIcon icon={faBuildingColumns} className="mr-2" />
                <span className="font-semibold">Stage actif :</span> {activeInternship.title} — {activeInternship.companyName}
              </div>
            )}
          </div>
          {isStudent && (
            <button
              onClick={() => setOpenSubmitDialog(true)} disabled={!activeInternship}
              className="flex items-center gap-2 px-4 py-2 bg-white text-blue-700 hover:bg-blue-50 rounded-lg font-semibold text-sm transition-colors disabled:opacity-50"
            >
              <FontAwesomeIcon icon={faPlus} /> Déposer un rapport
            </button>
          )}
        </div>
      </div>

      {/* Internship selector */}
      {isStudent && myInternships.length > 1 && (
        <div className="bg-white rounded-2xl p-4 border border-gray-200/80 shadow-sm flex items-center gap-4">
          <label className="flex items-center gap-2 font-semibold text-sm text-gray-700 whitespace-nowrap">
            <FontAwesomeIcon icon={faFolderOpen} className="text-blue-600" />
            Dossier de stage actif :
          </label>
          <select
            value={activeInternship?.id || ''}
            onChange={(e) => setSelectedInternshipId(e.target.value)}
            className="flex-1 px-4 py-2.5 bg-gray-50/70 hover:bg-white focus:bg-white border border-gray-200 focus:border-blue-500 rounded-xl text-sm text-gray-800 focus:outline-none focus:ring-4 focus:ring-blue-500/10 transition-all shadow-sm cursor-pointer"
          >
            {myInternships.map((i) => (
              <option key={i.id} value={i.id}>{i.title} - {i.companyName} ({i.statusName || i.status})</option>
            ))}
          </select>
        </div>
      )}

      {/* Reports */}
      {isLoading ? (
        <div className="flex justify-center py-16"><BusyIndicator active /></div>
      ) : reports.length === 0 ? (
        <div className="text-center py-16 bg-gray-50 rounded-2xl border-2 border-dashed border-gray-300">
          <FontAwesomeIcon icon={faClipboardList} className="text-5xl text-gray-300 mb-4" />
          <Title level="H3">{isStudent ? 'Aucun rapport déposé' : 'Aucun rapport à réviser'}</Title>
          <Text style={{ color: '#666', marginTop: '0.5rem', display: 'block' }}>
            {isStudent ? "Vous n'avez pas encore déposé de rapport de suivi hebdomadaire." : "Aucun rapport d'avancement n'a été soumis pour le moment."}
          </Text>
          {isStudent && (
            <div className="mt-6">
              <Button design="Emphasized" onClick={() => setOpenSubmitDialog(true)} disabled={!activeInternship}>
                <FontAwesomeIcon icon={faPlus} className="mr-1" /> Déposer votre premier rapport
              </Button>
            </div>
          )}
        </div>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-2">
            <Title level="H4">Rapports d'avancement</Title>
            <span className="px-2.5 py-0.5 bg-blue-100 text-blue-700 rounded-full text-xs font-semibold">
              {reports.length} rapport{reports.length > 1 ? 's' : ''}
            </span>
          </div>

          {reports.map((r) => {
            const isExpanded = !!expandedCards[r.id];
            const taskText = r.activitiesPerformed || '';
            const commentsCount = r.comments?.length || 0;
            const isValidated = r.status === 'Valide';

            return (
              <div key={r.id} className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                <div className={`h-1 ${isValidated ? 'bg-gradient-to-r from-green-400 to-emerald-400' : 'bg-gradient-to-r from-amber-400 to-yellow-300'}`} />
                <div className="p-5">
                  {/* Header Row */}
                  <div className="flex justify-between items-center flex-wrap gap-2 mb-3">
                    <div>
                      <div className="flex items-center gap-2 font-bold text-gray-900">
                        <FontAwesomeIcon icon={faCalendarDays} className="text-blue-500" />
                        Semaine {r.weekNumber}
                        <span className="text-sm font-normal text-gray-500 ml-1">
                          — {formatDate(r.createdAt)}
                        </span>
                      </div>
                      {isSupervisorOrHead && (
                        <div className="mt-0.5 text-xs text-gray-500">
                          <FontAwesomeIcon icon={faUser} className="mr-1" />
                          <span className="font-semibold">{r.studentName || 'Élève ingénieur'}</span>
                          {r.internshipTitle && <span className="ml-1 text-gray-400">· {r.internshipTitle}</span>}
                        </div>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className={`px-2.5 py-1 rounded-full text-xs font-bold ${isValidated ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700'}`}>
                        {isValidated ? <><FontAwesomeIcon icon={faCheck} className="mr-1" />Validé</> : 'Soumis'}
                      </span>
                      {commentsCount > 0 && (
                        <span className="px-2.5 py-1 bg-blue-50 text-blue-700 rounded-full text-xs font-semibold">
                          <FontAwesomeIcon icon={faComment} className="mr-1" />
                          {commentsCount} commentaire{commentsCount > 1 ? 's' : ''}
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Preview */}
                  <div className="bg-gray-50 rounded-lg px-4 py-3 mb-4 text-sm text-gray-700 leading-relaxed">
                    {taskText ? truncate(taskText, 150) : <span className="text-gray-400 italic">Aucune tâche renseignée.</span>}
                  </div>

                  {/* Expanded */}
                  {isExpanded && (
                    <div className="flex flex-col gap-3 mb-4">
                      <div>
                        <div className="text-xs font-semibold text-gray-500 mb-1">
                          <FontAwesomeIcon icon={faCheck} className="mr-1 text-green-500" /> Tâches accomplies
                        </div>
                        <div className="bg-green-50 rounded-lg p-3 text-sm whitespace-pre-wrap">{taskText || '—'}</div>
                      </div>
                      <div>
                        <div className="text-xs font-semibold text-gray-500 mb-1">Compétences acquises</div>
                        <div className="bg-purple-50 rounded-lg p-3 text-sm whitespace-pre-wrap">{r.skillsAcquired || '—'}</div>
                      </div>
                      <div>
                        <div className="text-xs font-semibold text-gray-500 mb-1">Objectifs semaine suivante</div>
                        <div className="bg-blue-50 rounded-lg p-3 text-sm whitespace-pre-wrap">
                          {r.nextWeekGoals || '—'}
                        </div>
                      </div>
                      <div>
                        <div className="text-xs font-semibold text-gray-500 mb-1">
                          <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1 text-amber-500" /> Difficultés rencontrées
                        </div>
                        <div className="bg-amber-50 rounded-lg p-3 text-sm whitespace-pre-wrap">
                          {r.difficultiesEncountered || '—'}
                        </div>
                      </div>
                      {commentsCount > 0 && (
                        <div>
                          <div className="text-xs font-semibold text-gray-500 mb-2">
                            <FontAwesomeIcon icon={faComment} className="mr-1 text-blue-500" /> Commentaires de l'encadrant
                          </div>
                          <div className="flex flex-col gap-2">
                            {r.comments!.map((c: any, idx: number) => (
                              <div key={idx} className="bg-gray-50 rounded-lg p-3 border-l-2 border-blue-500 text-sm">
                                <div className="font-semibold text-gray-800 mb-0.5">
                                  {c.authorName || c.userName || 'Encadrant'}
                                  <span className="font-normal text-gray-400 ml-1 text-xs">· {formatDate(c.createdAt)}</span>
                                </div>
                                <div className="text-gray-600">{c.content}</div>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  )}

                  {/* Actions */}
                  <div className="flex justify-between items-center flex-wrap gap-2">
                    <button
                      onClick={() => toggleCard(r.id)}
                      className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
                    >
                      <FontAwesomeIcon icon={isExpanded ? faChevronUp : faChevronDown} />
                      {isExpanded ? 'Masquer les détails' : 'Voir les détails'}
                    </button>
                    <div className="flex gap-2 flex-wrap">
                      {isSupervisorOrHead && (
                        <>
                          {!isValidated && (
                            <Button design="Positive" onClick={() => validateReportMutation.mutate(r.id)} disabled={validateReportMutation.isPending}>
                              <FontAwesomeIcon icon={faCheck} className="mr-1" /> Valider
                            </Button>
                          )}
                          <Button design="Default" onClick={() => { setSelectedReport(r); setOpenCommentDialog(true); }}>
                            <FontAwesomeIcon icon={faComment} className="mr-1" /> Commenter
                          </Button>
                          <Button design="Transparent" onClick={() => handleSummarizeWithAi(r)}>
                            <FontAwesomeIcon icon={faRobot} className="mr-1" /> Résumé IA
                          </Button>
                        </>
                      )}
                      {isStudent && (
                        <>
                          {!isValidated && (
                            <Button design="Default" onClick={() => openEditDialog(r)}>
                              <FontAwesomeIcon icon={faPenToSquare} className="mr-1" /> Modifier
                            </Button>
                          )}
                          {commentsCount > 0 && (
                            <Button design="Default" onClick={() => { setSelectedReport(r); setOpenCommentDialog(true); }}>
                              <FontAwesomeIcon icon={faComment} className="mr-1" /> Répondre
                            </Button>
                          )}
                        </>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Submit Report Dialog */}
      <Dialog open={openSubmitDialog} headerText={editingReportId ? `Modifier le rapport — semaine ${weekNumber}` : "Nouveau rapport hebdomadaire"}
        footer={
          <Bar endContent={
            <>
              <Button design="Transparent" onClick={closeSubmitDialog}>Annuler</Button>
              <Button design="Emphasized"
                disabled={!activities.trim() || submitReportMutation.isPending}
                onClick={() => submitReportMutation.mutate()}
              >
                {submitReportMutation.isPending ? 'Enregistrement...' : editingReportId ? 'Enregistrer les modifications' : 'Déposer le rapport'}
              </Button>
            </>
          } />
        }
      >
        <div className="flex flex-col gap-4 p-4" style={{ minWidth: '450px' }}>
          {submitError && (
            <div className="p-2.5 bg-red-50 text-red-700 rounded text-sm">{submitError}</div>
          )}
          <div>
            <label className="block mb-1 font-semibold text-sm">Numéro de semaine :</label>
            <Input type="Number" value={weekNumber.toString()} disabled={!!editingReportId}
              onInput={(e: any) => setWeekNumber(Number(e.target.value))} style={{ width: '100%' }} />
            {activeInternship?.totalWeeks && (
              <div className="text-xs text-gray-500 mt-1">
                Semaines 1 à {activeInternship.totalWeeks} ; seule une semaine déjà commencée peut faire l'objet d'un rapport.
              </div>
            )}
          </div>
          <div>
            <label className="block mb-1 font-semibold text-sm">Titre du rapport (Optionnel) :</label>
            <Input value={reportTitle} onInput={(e: any) => setReportTitle(e.target.value)}
              placeholder="Ex: Immersion et prise en main de l'architecture" style={{ width: '100%' }} />
          </div>
          <div>
            <label className="block mb-1 font-semibold text-sm">Activités &amp; Tâches accomplies (obligatoire) :</label>
            <TextArea rows={3} value={activities} onInput={(e: any) => setActivities(e.target.value)}
              placeholder="Description des réalisations techniques, tickets résolus..." style={{ width: '100%' }} />
          </div>
          <div>
            <label className="block mb-1 font-semibold text-sm">Compétences acquises :</label>
            <TextArea rows={2} value={skills} onInput={(e: any) => setSkills(e.target.value)}
              placeholder="Technologies, méthodes, savoir-être..." style={{ width: '100%' }} />
          </div>
          <div>
            <label className="block mb-1 font-semibold text-sm">Objectifs prévus (semaine suivante) :</label>
            <TextArea rows={2} value={nextGoals} onInput={(e: any) => setNextGoals(e.target.value)}
              placeholder="Livrables et étapes prévues..." style={{ width: '100%' }} />
          </div>
          <div>
            <label className="block mb-1 font-semibold text-sm">Difficultés rencontrées :</label>
            <TextArea rows={2} value={difficulties} onInput={(e: any) => setDifficulties(e.target.value)}
              placeholder="Blocages, questions techniques ou organisationnelles..." style={{ width: '100%' }} />
          </div>
        </div>
      </Dialog>

      {/* Comment Dialog */}
      <Dialog open={openCommentDialog} headerText={isStudent ? `Répondre à votre encadrant — Semaine ${selectedReport?.weekNumber}` : `Commentaire pédagogique — Semaine ${selectedReport?.weekNumber}`}
        footer={
          <Bar endContent={
            <>
              <Button design="Transparent" onClick={() => setOpenCommentDialog(false)}>Annuler</Button>
              <Button design="Emphasized" disabled={!commentText || addCommentMutation.isPending}
                onClick={() => addCommentMutation.mutate()}>
                Envoyer le commentaire
              </Button>
            </>
          } />
        }
      >
        <div className="flex flex-col gap-4 p-4" style={{ minWidth: '400px' }}>
          <div>
            <label className="block mb-1 font-semibold text-sm">{isStudent ? 'Votre réponse :' : 'Votre feedback :'}</label>
            <TextArea rows={4} value={commentText} onInput={(e: any) => setCommentText(e.target.value)}
              placeholder="Conseils, remarques sur l'avancement..." style={{ width: '100%' }} />
          </div>
        </div>
      </Dialog>

      {/* AI Summary Dialog */}
      <Dialog open={openAiDialog} headerText={`Résumé Intelligent IA (Google Gemini) — Semaine ${selectedReport?.weekNumber}`}
        footer={
          <Bar endContent={
            <Button design="Emphasized" onClick={() => setOpenAiDialog(false)}>Fermer</Button>
          } />
        }
      >
        <div className="p-4 leading-relaxed" style={{ minWidth: '450px' }}>
          {aiLoading ? (
            <div className="flex items-center gap-4 py-8">
              <BusyIndicator active />
              <span>Analyse et synthèse par le modèle Gemini en cours...</span>
            </div>
          ) : (
            <div>
              <div className="text-xs text-gray-500 mb-3">
                Synthèse générée via l'abstraction <code>IAiSummaryService</code> :
              </div>
              <div className="bg-gray-50 p-4 rounded-lg border border-gray-200 whitespace-pre-wrap text-sm">
                {aiSummary}
              </div>
            </div>
          )}
        </div>
      </Dialog>
    </div>
  );
};
export default ReportsPage;
