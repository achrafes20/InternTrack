import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Card, CardHeader, Button, Table, TableHeaderRow, TableHeaderCell,
  TableRow, TableCell, Tag, Dialog, Input, Text, BusyIndicator, Bar, Title
} from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faGraduationCap, faCalendar, faLocationDot, faCrown,
  faMagnifyingGlass, faPlus, faCheck, faTimes, faTriangleExclamation,
  faUsers, faClock
} from '@fortawesome/free-solid-svg-icons';
import { apiClient } from '../../api/client';
import { DefenseDto, DefenseConflictCheckResult, InternshipDto } from '../../types';
import { useAuth } from '../../context/AuthContext';

export const DefensesPage: React.FC = () => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const isCoord = user?.role === 'Admin' || user?.role === 'ResponsableFiliere';

  const [openModal, setOpenModal] = useState(false);
  const [internshipId, setInternshipId] = useState('');
  const [defenseDate, setDefenseDate] = useState('');
  const [classroom, setClassroom] = useState('');
  const [juryPresident, setJuryPresident] = useState('Karim Alami');
  const [examiner, setExaminer] = useState('Mohamed Tazi');
  const [guest, setGuest] = useState('');
  const [conflictResult, setConflictResult] = useState<DefenseConflictCheckResult | null>(null);
  const [scheduleError, setScheduleError] = useState('');

  const { data: defenses = [], isLoading } = useQuery<DefenseDto[]>({
    queryKey: ['defenses'],
    queryFn: async () => { const res = await apiClient.get('/defenses'); return res.data; }
  });

  const { data: eligibleInternships = [] } = useQuery<InternshipDto[]>({
    queryKey: ['eligible-internships'],
    queryFn: async () => {
      const res = await apiClient.get('/internships');
      return (res.data as InternshipDto[]).filter(i => {
        const st = (i.statusName || i.status || '').toString();
        return st === 'EnCours' || st === 'SoutenancePlanifiee' || [5, 6].includes(Number(i.status));
      });
    },
    enabled: isCoord
  });

  const checkConflictMutation = useMutation({
    mutationFn: async () => {
      setScheduleError('');
      const juryNames = [juryPresident, examiner, guest].filter(Boolean);
      const res = await apiClient.post<DefenseConflictCheckResult>('/defenses/check-conflict', {
        defenseDate: new Date(defenseDate).toISOString(), classroom, juryTeacherNames: juryNames
      });
      return res.data;
    },
    onSuccess: (data) => setConflictResult(data),
    onError: (err: any) => setScheduleError(err.response?.data?.detail || 'Erreur lors de la vérification des conflits.')
  });

  const scheduleMutation = useMutation({
    mutationFn: async () => {
      setScheduleError('');
      const juryMembers = [
        { teacherFullName: juryPresident, teacherId: juryPresident, role: 0 },
        { teacherFullName: examiner, teacherId: examiner, role: 1 },
        guest ? { teacherFullName: guest, teacherId: guest, role: 2 } : null
      ].filter(Boolean);
      await apiClient.post('/defenses', {
        internshipId, scheduledAt: new Date(defenseDate).toISOString(),
        durationMinutes: 45, classroom, juryMembers
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['defenses'] });
      queryClient.invalidateQueries({ queryKey: ['internships'] });
      queryClient.invalidateQueries({ queryKey: ['eligible-internships'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
      setOpenModal(false); resetForm();
    },
    onError: (err: any) => setScheduleError(err.response?.data?.detail || 'Erreur lors de la planification de la soutenance.')
  });

  const resetForm = () => {
    setInternshipId(''); setDefenseDate(''); setClassroom('');
    setJuryPresident('Karim Alami'); setExaminer('Mohamed Tazi');
    setGuest(''); setConflictResult(null); setScheduleError('');
  };

  const handleOpenDialog = () => {
    resetForm();
    if (eligibleInternships.length > 0) setInternshipId(eligibleInternships[0].id);
    setOpenModal(true);
  };

  const inputStyle = {
    width: '100%', padding: '0.5rem', borderRadius: '4px',
    border: '1px solid #ccc', boxSizing: 'border-box' as const, fontSize: '14px'
  };

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
                      <small className="text-gray-500">{d.internshipTitle || d.internshipSubject}</small>
                    </div>
                  </TableCell>
                  <TableCell>
                    <span className="font-semibold text-blue-600 flex items-center gap-1 text-sm">
                      <FontAwesomeIcon icon={faCalendar} />
                      {new Date(d.scheduledAt || d.defenseDate || '').toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' })}
                    </span>
                  </TableCell>
                  <TableCell>
                    <span className="flex items-center gap-1 text-sm">
                      <FontAwesomeIcon icon={faLocationDot} className="text-blue-500" />
                      <Tag colorScheme="1">Salle {d.classroom}</Tag>
                    </span>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      {d.juryMembers.map((m: any, idx: number) => {
                        const roleLabel = m.roleName || (m.role === 0 ? 'Président' : m.role === 1 ? 'Examinateur' : 'Invité');
                        const icon = m.role === 0 ? faCrown : m.role === 1 ? faMagnifyingGlass : faUsers;
                        return (
                          <span key={idx} className="text-xs flex items-center gap-1">
                            <FontAwesomeIcon icon={icon} className="text-gray-400" />
                            <strong>{roleLabel}:</strong> {m.teacherFullName || m.teacherName}
                          </span>
                        );
                      })}
                    </div>
                  </TableCell>
                  <TableCell>
                    {(d.finalGrade ?? d.grade) !== undefined && (d.finalGrade ?? d.grade) !== null ? (
                      <Tag colorScheme="8">{(d.finalGrade ?? d.grade)}/20</Tag>
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
                disabled={!defenseDate || !classroom || !juryPresident || !examiner || checkConflictMutation.isPending}
              >
                <FontAwesomeIcon icon={faMagnifyingGlass} className="mr-1" />
                {checkConflictMutation.isPending ? 'Vérification...' : 'Vérifier les conflits'}
              </Button>
              <Button design="Emphasized"
                disabled={
                  !internshipId || !defenseDate || !classroom || !juryPresident || !examiner ||
                  (conflictResult && conflictResult.hasConflict) || scheduleMutation.isPending
                }
                onClick={() => scheduleMutation.mutate()}
              >
                {scheduleMutation.isPending ? 'Planification...' : 'Confirmer la soutenance'}
              </Button>
            </>
          } />
        }
      >
        <div className="flex flex-col gap-4 p-4" style={{ minWidth: '420px' }}>
          {scheduleError && (
            <div className="p-2.5 bg-red-50 text-red-700 rounded text-sm">
              <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {scheduleError}
            </div>
          )}

          <div>
            <label className="block mb-1 font-semibold text-sm">Stage PFE à planifier :</label>
            <select style={inputStyle} value={internshipId} onChange={(e) => setInternshipId(e.target.value)}>
              <option value="">-- Sélectionner un dossier éligible --</option>
              {eligibleInternships.map(i => (
                <option key={i.id} value={i.id}>{i.title} ({i.companyName}) - {i.statusName || i.status}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">Date et Heure :</label>
            <input type="datetime-local" value={defenseDate}
              onChange={(e) => setDefenseDate(e.target.value)} style={inputStyle} />
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">Salle d'examen :</label>
            <Input value={classroom} onInput={(e: any) => setClassroom(e.target.value)}
              placeholder="Ex: Salle des Actes, Salle 204, Amphi B" style={{ width: '100%' }} />
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">
              <FontAwesomeIcon icon={faCrown} className="mr-1 text-amber-500" /> Président du jury :
            </label>
            <Input value={juryPresident} onInput={(e: any) => setJuryPresident(e.target.value)}
              placeholder="Ex: Karim Alami" style={{ width: '100%' }} />
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">
              <FontAwesomeIcon icon={faMagnifyingGlass} className="mr-1 text-blue-500" /> Examinateur :
            </label>
            <Input value={examiner} onInput={(e: any) => setExaminer(e.target.value)}
              placeholder="Ex: Mohamed Tazi" style={{ width: '100%' }} />
          </div>

          <div>
            <label className="block mb-1 font-semibold text-sm">Invité / Encadrant (Optionnel) :</label>
            <Input value={guest} onInput={(e: any) => setGuest(e.target.value)}
              placeholder="Tuteur ou invité extérieur" style={{ width: '100%' }} />
          </div>

          {conflictResult && (
            <div className={`p-3 rounded-lg border text-sm ${conflictResult.hasConflict ? 'bg-red-50 border-red-300' : 'bg-green-50 border-green-300'}`}>
              <div className={`font-semibold flex items-center gap-1 ${conflictResult.hasConflict ? 'text-red-800' : 'text-green-800'}`}>
                {conflictResult.hasConflict
                  ? <><FontAwesomeIcon icon={faTriangleExclamation} /> Conflit détecté !</>
                  : <><FontAwesomeIcon icon={faCheck} /> Créneau disponible (Aucun conflit) !</>
                }
              </div>
              {conflictResult.conflictMessages?.length > 0 && (
                <ul className="mt-2 ml-4 text-xs text-red-700">
                  {conflictResult.conflictMessages.map((m: string, idx: number) => <li key={idx}>{m}</li>)}
                </ul>
              )}
            </div>
          )}
        </div>
      </Dialog>
    </div>
  );
};
export default DefensesPage;
