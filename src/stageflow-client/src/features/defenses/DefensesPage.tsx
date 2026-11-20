import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Card, CardHeader, Button, Table, TableHeaderRow, TableHeaderCell,
  TableRow, TableCell, Tag, Dialog, Input, Text, BusyIndicator, Bar, Title, TextArea
} from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faGraduationCap, faCalendar, faLocationDot, faCrown,
  faMagnifyingGlass, faPlus, faCheck, faTriangleExclamation,
  faUsers, faClock, faPenToSquare, faBuilding
} from '@fortawesome/free-solid-svg-icons';
import { apiClient } from '../../api/client';
import {
  DefenseDto, DefenseConflictCheckResult, InternshipDto, Teacher, DefenseRole,
  DEFENSE_ROLE_LABELS, apiErrorMessage
} from '../../types';
import { useAuth } from '../../context/AuthContext';

const ROLE_ICONS: Record<DefenseRole, any> = {
  President: faCrown,
  Rapporteur: faMagnifyingGlass,
  Examinateur: faUsers,
  TuteurEntreprise: faBuilding,
};

export const DefensesPage: React.FC = () => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const isCoord = user?.role === 'Admin' || user?.role === 'ResponsableFiliere';

  const [openModal, setOpenModal] = useState(false);
  const [internshipId, setInternshipId] = useState('');
  const [defenseDate, setDefenseDate] = useState('');
  const [durationMinutes, setDurationMinutes] = useState(45);
  const [classroom, setClassroom] = useState('');
  const [presidentId, setPresidentId] = useState('');
  const [rapporteurId, setRapporteurId] = useState('');
  const [examinerId, setExaminerId] = useState('');
  const [companyTutor, setCompanyTutor] = useState('');
  const [conflictResult, setConflictResult] = useState<DefenseConflictCheckResult | null>(null);
  const [scheduleError, setScheduleError] = useState('');

  const [gradeDefense, setGradeDefense] = useState<DefenseDto | null>(null);
  const [grade, setGrade] = useState('');
  const [gradeComments, setGradeComments] = useState('');
  const [gradeError, setGradeError] = useState('');

  const { data: defenses = [], isLoading } = useQuery<DefenseDto[]>({
    queryKey: ['defenses'],
    queryFn: async () => (await apiClient.get('/defenses')).data
  });

  const { data: eligibleInternships = [] } = useQuery<InternshipDto[]>({
    queryKey: ['eligible-internships'],
    queryFn: async () => {
      const res = await apiClient.get<InternshipDto[]>('/internships');
      return res.data.filter(i => i.statusName === 'EnCours' || i.statusName === 'SoutenancePlanifiee');
    },
    enabled: isCoord
  });

  const { data: teachers = [] } = useQuery<Teacher[]>({
    queryKey: ['teachers'],
    queryFn: async () => (await apiClient.get('/teachers')).data,
    enabled: isCoord
  });

  const internalJury = (): Array<{ teacherId: string; role: DefenseRole }> =>
    ([
      { teacherId: presidentId, role: 'President' },
      { teacherId: rapporteurId, role: 'Rapporteur' },
      { teacherId: examinerId, role: 'Examinateur' },
    ] as Array<{ teacherId: string; role: DefenseRole }>).filter(m => m.teacherId);

  const scheduledAtIso = () => (defenseDate ? new Date(defenseDate).toISOString() : '');

  const checkConflictMutation = useMutation({
    mutationFn: async () => {
      setScheduleError('');
      const res = await apiClient.post<DefenseConflictCheckResult>('/defenses/check-conflict', {
        scheduledAt: scheduledAtIso(),
        durationMinutes,
        classroom,
        juryTeacherIds: internalJury().map(m => m.teacherId),
        internshipId: internshipId || undefined,
      });
      return res.data;
    },
    onSuccess: (data) => setConflictResult(data),
    onError: (err: any) => setScheduleError(apiErrorMessage(err, 'Erreur lors de la vérification des conflits.'))
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ['defenses'] });
    queryClient.invalidateQueries({ queryKey: ['internships'] });
    queryClient.invalidateQueries({ queryKey: ['eligible-internships'] });
    queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
  };

  const scheduleMutation = useMutation({
    mutationFn: async () => {
      setScheduleError('');
      const juryMembers: Array<{ teacherId?: string; teacherFullName?: string; role: DefenseRole }> = internalJury();
      if (companyTutor.trim()) juryMembers.push({ teacherFullName: companyTutor.trim(), role: 'TuteurEntreprise' });

      await apiClient.post('/defenses', {
        internshipId,
        scheduledAt: scheduledAtIso(),
        durationMinutes,
        classroom,
        juryMembers
      });
    },
    onSuccess: () => { invalidateAll(); setOpenModal(false); resetForm(); },
    onError: (err: any) => setScheduleError(apiErrorMessage(err, 'Erreur lors de la planification de la soutenance.'))
  });

  const gradeMutation = useMutation({
    mutationFn: async () => {
      if (!gradeDefense) return;
      setGradeError('');
      await apiClient.post(`/defenses/${gradeDefense.id}/grade`, {
        grade: Number(grade.replace(',', '.')),
        comments: gradeComments || null
      });
    },
    onSuccess: () => { invalidateAll(); setGradeDefense(null); },
    onError: (err: any) => setGradeError(apiErrorMessage(err, 'Erreur lors de la saisie de la note.'))
  });

  const resetForm = () => {
    setInternshipId(''); setDefenseDate(''); setClassroom(''); setDurationMinutes(45);
    setPresidentId(''); setRapporteurId(''); setExaminerId(''); setCompanyTutor('');
    setConflictResult(null); setScheduleError('');
  };

  const handleOpenDialog = () => {
    resetForm();
    if (eligibleInternships.length > 0) {
      setInternshipId(eligibleInternships[0].id);
      setCompanyTutor(eligibleInternships[0].companySupervisorName || '');
    }
    setOpenModal(true);
  };

  const handleSelectInternship = (id: string) => {
    setInternshipId(id);
    setConflictResult(null);
    const selected = eligibleInternships.find(i => i.id === id);
    if (selected) setCompanyTutor(selected.companySupervisorName || '');
  };

  const openGradeDialog = (d: DefenseDto) => {
    setGradeDefense(d); setGrade(''); setGradeComments(''); setGradeError('');
  };

  const juryValid = !!presidentId && internalJury().length + (companyTutor.trim() ? 1 : 0) >= 2;
  const formReady = !!internshipId && !!defenseDate && !!classroom.trim() && juryValid;

  const inputStyle = {
    width: '100%', padding: '0.5rem', borderRadius: '4px',
    border: '1px solid #ccc', boxSizing: 'border-box' as const, fontSize: '14px'
  };

  const teacherSelect = (value: string, onChange: (v: string) => void, exclude: string[], placeholder: string) => (
      <select style={inputStyle} value={value} onChange={(e) => { onChange(e.target.value); setConflictResult(null); }}>
        <option value="">{placeholder}</option>
        {teachers.filter(t => t.id === value || !exclude.includes(t.id)).map(t => (
          <option key={t.id} value={t.id}>{t.fullName} ({t.role === 'ResponsableFiliere' ? 'Responsable' : 'Encadrant'})</option>
        ))}
      </select>
    );

  const canGrade = (d: DefenseDto) =>
    isCoord && (d.finalGrade === undefined || d.finalGrade === null) && new Date(d.scheduledAt) <= new Date();

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex justify-between items-center flex-wrap gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <FontAwesomeIcon icon={faGraduationCap} className="text-blue-600 text-xl" />
            <Title level="H2">Planning des Soutenances de PFE</Title>
          </div>
          <Text style={{ color: '#666' }}>
            Consultez le planning et planifiez les soutenances avec détection automatique de conflits de salle et de jury.
          </Text>
        </div>
        {isCoord && (
          <button
            onClick={handleOpenDialog}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-semibold text-sm transition-colors"
          >
            <FontAwesomeIcon icon={faPlus} /> Planifier une soutenance
          </button>
        )}
      </div>

      {/* Table Card */}
      <Card header={<CardHeader titleText={`Soutenances planifiées (${defenses.length})`} />}>
        <div style={{ padding: '1rem' }}>
          {isLoading ? (
            <div className="flex justify-center py-8"><BusyIndicator active /></div>
          ) : defenses.length === 0 ? (
            <div className="text-center py-16 text-gray-500">
              <FontAwesomeIcon icon={faGraduationCap} className="text-5xl text-gray-300 mb-3" />
              <div>Aucune soutenance programmée pour le moment.</div>
            </div>
          ) : (
            <Table>
              <TableHeaderRow slot="headerRow">
                <TableHeaderCell><span>Élève Ingénieur &amp; Projet</span></TableHeaderCell>
                <TableHeaderCell><span>Date &amp; Heure</span></TableHeaderCell>
                <TableHeaderCell><span>Salle</span></TableHeaderCell>
                <TableHeaderCell><span>Composition du Jury</span></TableHeaderCell>
                <TableHeaderCell><span>Note Finale</span></TableHeaderCell>
              </TableHeaderRow>
              {defenses.map(d => (
                <TableRow key={d.id}>
                  <TableCell>
                    <div>
                      <span className="font-semibold block">{d.studentName}</span>
                      <small className="text-gray-500">{d.internshipTitle}</small>
                    </div>
                  </TableCell>
                  <TableCell>
                    <span className="font-semibold text-blue-600 flex items-center gap-1 text-sm">
                      <FontAwesomeIcon icon={faCalendar} />
                      {new Date(d.scheduledAt).toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' })}
                      <span className="text-gray-400 font-normal">({d.durationMinutes} min)</span>
                    </span>
                  </TableCell>
                  <TableCell>
                    <span className="flex items-center gap-1 text-sm">
                      <FontAwesomeIcon icon={faLocationDot} className="text-blue-500" />
                      <Tag colorScheme="1">{d.classroom}</Tag>
                    </span>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      {d.juryMembers.map(m => (
                        <span key={m.id} className="text-xs flex items-center gap-1">
                          <FontAwesomeIcon icon={ROLE_ICONS[m.role] ?? faUsers} className="text-gray-400" />
                          <strong>{DEFENSE_ROLE_LABELS[m.role] ?? m.roleName} :</strong> {m.teacherFullName}
                        </span>
                      ))}
                    </div>
                  </TableCell>
                  <TableCell>
                    {d.finalGrade !== undefined && d.finalGrade !== null ? (
                      <Tag colorScheme="8">{d.finalGrade}/20</Tag>
                    ) : canGrade(d) ? (
                      <Button design="Emphasized" onClick={() => openGradeDialog(d)}>
                        <FontAwesomeIcon icon={faPenToSquare} className="mr-1" /> Saisir la note
                      </Button>
                    ) : (
                      <span className="flex items-center gap-1 text-xs text-gray-400">
                        <FontAwesomeIcon icon={faClock} /> En attente
                      </span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </Table>
          )}
        </div>
      </Card>

      {/* Schedule Dialog */}
      <Dialog open={openModal} headerText="Planifier une soutenance de PFE"
        footer={
          <Bar endContent={
            <>
              <Button design="Transparent" onClick={() => setOpenModal(false)}>Annuler</Button>
              <Button design="Attention"
                onClick={() => checkConflictMutation.mutate()}
                disabled={!defenseDate || !classroom.trim() || checkConflictMutation.isPending}
              >
                <FontAwesomeIcon icon={faMagnifyingGlass} className="mr-1" />
                {checkConflictMutation.isPending ? 'Vérification...' : 'Vérifier les conflits'}
              </Button>
              <Button design="Emphasized"
                disabled={!formReady || (conflictResult?.hasConflict ?? false) || scheduleMutation.isPending}
                onClick={() => scheduleMutation.mutate()}
              >
                {scheduleMutation.isPending ? 'Planification...' : 'Confirmer la soutenance'}
              </Button>
            </>
          } />
        }
      >
        <div className="flex flex-col gap-4 p-4" style={{ minWidth: '440px' }}>
          {scheduleError && (
            <div className="p-2.5 bg-red-50 text-red-700 rounded text-sm">
              <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {scheduleError}
            </div>
          )}

          <div>
            <label className="block mb-1 font-semibold text-sm">Stage PFE à planifier :</label>
            <select style={inputStyle} value={internshipId} onChange={(e) => handleSelectInternship(e.target.value)}>
              <option value="">-- Sélectionner un dossier éligible --</option>
              {eligibleInternships.map(i => (
                <option key={i.id} value={i.id}>{i.studentName ? `${i.studentName} — ` : ''}{i.title} ({i.companyName})</option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block mb-1 font-semibold text-sm">Date et heure :</label>
              <input type="datetime-local" value={defenseDate}
                onChange={(e) => { setDefenseDate(e.target.value); setConflictResult(null); }} style={inputStyle} />
            </div>
            <div>
              <label className="block mb-1 font-semibold text-sm">Durée (minutes) :</label>
              <input type="number" min={15} max={240} step={15} value={durationMinutes}
                onChange={(e) => { setDurationMinutes(Number(e.target.value)); setConflictResult(null); }} style={inputStyle} />
            </div>
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">Salle d'examen :</label>
            <Input value={classroom} onInput={(e: any) => { setClassroom(e.target.value); setConflictResult(null); }}
              placeholder="Ex: Salle des Actes, Salle 204, Amphi B" style={{ width: '100%' }} />
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">
              <FontAwesomeIcon icon={faCrown} className="mr-1 text-amber-500" /> Président du jury (obligatoire) :
            </label>
            {teacherSelect(presidentId, setPresidentId, [rapporteurId, examinerId], '-- Choisir un enseignant --')}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block mb-1 font-semibold text-sm">Rapporteur :</label>
              {teacherSelect(rapporteurId, setRapporteurId, [presidentId, examinerId], '-- Aucun --')}
            </div>
            <div>
              <label className="block mb-1 font-semibold text-sm">Examinateur :</label>
              {teacherSelect(examinerId, setExaminerId, [presidentId, rapporteurId], '-- Aucun --')}
            </div>
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">
              <FontAwesomeIcon icon={faBuilding} className="mr-1 text-gray-500" /> Tuteur entreprise (membre externe, optionnel) :
            </label>
            <Input value={companyTutor} onInput={(e: any) => setCompanyTutor(e.target.value)}
              placeholder="Nom du tuteur en entreprise" style={{ width: '100%' }} />
          </div>

          {!juryValid && (
            <div className="text-xs text-gray-500">Le jury doit comporter un président et au moins 2 membres au total (4 maximum).</div>
          )}

          {conflictResult && (
            <div className={`p-3 rounded-lg border text-sm ${conflictResult.hasConflict ? 'bg-red-50 border-red-300' : 'bg-green-50 border-green-300'}`}>
              <div className={`font-semibold flex items-center gap-1 ${conflictResult.hasConflict ? 'text-red-800' : 'text-green-800'}`}>
                {conflictResult.hasConflict
                  ? <><FontAwesomeIcon icon={faTriangleExclamation} /> Conflit détecté !</>
                  : <><FontAwesomeIcon icon={faCheck} /> Créneau disponible (aucun conflit)</>
                }
              </div>
              {conflictResult.conflictMessages.length > 0 && (
                <ul className="mt-2 ml-4 text-xs text-red-700">
                  {conflictResult.conflictMessages.map((m, idx) => <li key={idx}>{m}</li>)}
                </ul>
              )}
            </div>
          )}
        </div>
      </Dialog>

      {/* Grade Dialog */}
      {gradeDefense && (
        <Dialog open headerText="Saisie de la note finale"
          footer={
            <Bar endContent={
              <>
                <Button design="Transparent" onClick={() => setGradeDefense(null)}>Annuler</Button>
                <Button design="Emphasized" onClick={() => gradeMutation.mutate()}
                  disabled={grade === '' || gradeMutation.isPending}>
                  {gradeMutation.isPending ? 'Enregistrement...' : 'Enregistrer et clôturer le stage'}
                </Button>
              </>
            } />
          }
        >
          <div className="flex flex-col gap-3 p-4" style={{ minWidth: '380px' }}>
            {gradeError && (
              <div className="p-2.5 bg-red-50 text-red-700 rounded text-sm">
                <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {gradeError}
              </div>
            )}
            <Text><strong>{gradeDefense.studentName}</strong> — {gradeDefense.internshipTitle}</Text>
            <div>
              <label className="block mb-1 font-semibold text-sm">Note sur 20 :</label>
              <input type="number" min={0} max={20} step={0.25} value={grade}
                onChange={(e) => setGrade(e.target.value)} style={inputStyle} />
            </div>
            <div>
              <label className="block mb-1 font-semibold text-sm">Appréciation du jury (optionnel) :</label>
              <TextArea rows={3} value={gradeComments} onInput={(e: any) => setGradeComments(e.target.value)} style={{ width: '100%' }} />
            </div>
            <Text style={{ fontSize: '12px', color: '#666' }}>
              L'enregistrement de la note clôture définitivement le dossier (statut « Terminé »).
            </Text>
          </div>
        </Dialog>
      )}
    </div>
  );
};
export default DefensesPage;
