import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bar, Button, Dialog, Input, Text, TextArea } from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCheck, faFileSignature, faHourglass, faTriangleExclamation } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { ConventionAmendment, Internship, apiErrorMessage } from '../../types';

interface Props {
  internship: Internship;
  isStudent: boolean;
  isCoord: boolean;
}

const STATUS_LABELS: Record<ConventionAmendment['status'], { label: string; className: string }> = {
  EnAttente: { label: 'en attente de signatures', className: 'text-amber-700' },
  Applique: { label: 'appliqué', className: 'text-green-700' },
  Retire: { label: 'retiré', className: 'text-gray-500' },
};

const toDateInput = (iso: string) => iso.substring(0, 10);
const toIsoDate = (value: string) => (value ? new Date(`${value}T00:00:00Z`).toISOString() : undefined);

/**
 * Avenants à la convention signée : changement de dates, de tuteur entreprise ou de sujet.
 * L'école crée l'avenant (et le signe), l'élève le signe depuis son compte, l'école enregistre la signature
 * de l'entreprise ; les modifications s'appliquent au dossier à la troisième signature.
 */
export const AmendmentsPanel: React.FC<Props> = ({ internship, isStudent, isCoord }) => {
  const queryClient = useQueryClient();
  const amendable = ['Valide', 'EnCours'].includes(internship.statusName || internship.status)
    && !internship.convention?.terminatedAt;

  const { data: amendments = [] } = useQuery<ConventionAmendment[]>({
    queryKey: ['amendments', internship.id],
    queryFn: async () => (await api.get(`/conventions/internship/${internship.id}/amendments`)).data,
  });

  const [error, setError] = useState('');
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [title, setTitle] = useState('');
  const [tutorName, setTutorName] = useState('');
  const [tutorEmail, setTutorEmail] = useState('');
  const [tutorPhone, setTutorPhone] = useState('');
  const [companySigner, setCompanySigner] = useState<{ id: string; name: string } | null>(null);

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['amendments', internship.id] });
    queryClient.invalidateQueries({ queryKey: ['internships'] });
  };

  const openCreate = () => {
    setReason('');
    setStartDate(toDateInput(internship.startDate));
    setEndDate(toDateInput(internship.endDate));
    setTitle(internship.title);
    setTutorName(internship.companySupervisorName);
    setTutorEmail(internship.companySupervisorEmail);
    setTutorPhone(internship.companySupervisorPhone ?? '');
    setError('');
    setOpen(true);
  };

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/conventions/internship/${internship.id}/amendments`, {
        reason,
        newStartDate: startDate !== toDateInput(internship.startDate) ? toIsoDate(startDate) : undefined,
        newEndDate: endDate !== toDateInput(internship.endDate) ? toIsoDate(endDate) : undefined,
        newTitle: title !== internship.title ? title : undefined,
        newCompanySupervisorName: tutorName !== internship.companySupervisorName ? tutorName : undefined,
        newCompanySupervisorEmail: tutorEmail !== internship.companySupervisorEmail ? tutorEmail : undefined,
        newCompanySupervisorPhone: tutorPhone !== (internship.companySupervisorPhone ?? '') ? tutorPhone : undefined,
      });
    },
    onSuccess: () => { refresh(); setOpen(false); },
    onError: (err: any) => setError(apiErrorMessage(err, "Création de l'avenant impossible.")),
  });

  const signMutation = useMutation({
    mutationFn: async ({ id, party, signerName }: { id: string; party: 'Student' | 'Company'; signerName?: string }) => {
      setError('');
      await api.post(`/conventions/internship/${internship.id}/amendments/${id}/sign`, { party, signerName });
    },
    onSuccess: () => { refresh(); setCompanySigner(null); },
    onError: (err: any) => setError(apiErrorMessage(err, "Signature de l'avenant impossible.")),
  });

  const withdrawMutation = useMutation({
    mutationFn: async (id: string) => {
      setError('');
      await api.post(`/conventions/internship/${internship.id}/amendments/${id}/withdraw`, { reason: "Retiré par l'école." });
    },
    onSuccess: refresh,
    onError: (err: any) => setError(apiErrorMessage(err, "Retrait de l'avenant impossible.")),
  });

  const pending = amendments.find(a => a.status === 'EnAttente');
  if (amendments.length === 0 && !(isCoord && amendable)) return null;

  const inputStyle = { width: '100%', padding: '6px 10px', borderRadius: '4px', border: '1px solid #ccc', boxSizing: 'border-box' as const };

  return (
    <div className="mt-2 p-2.5 bg-indigo-50 border border-indigo-200 rounded text-xs text-slate-700">
      <div className="flex items-center justify-between gap-2 mb-1">
        <span className="font-semibold flex items-center gap-1">
          <FontAwesomeIcon icon={faFileSignature} /> Avenants à la convention ({amendments.length})
        </span>
        {isCoord && amendable && !pending && (
          <Button design="Transparent" onClick={openCreate}>Nouvel avenant</Button>
        )}
      </div>

      {error && (
        <div className="p-2 mb-1 bg-red-50 text-red-700 rounded">
          <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {error}
        </div>
      )}

      {amendments.map(a => (
        <div key={a.id} className="py-1.5 border-t border-indigo-100 first:border-t-0">
          <div>
            <strong>Avenant n°{a.number}</strong> — <span className={STATUS_LABELS[a.status].className}>{STATUS_LABELS[a.status].label}</span>
            <span className="text-gray-500"> · {a.reason}</span>
          </div>
          <div className="whitespace-pre-line text-gray-600">{a.changesSummary}</div>
          {a.status === 'EnAttente' && (
            <>
              <div className="flex gap-3 flex-wrap mt-0.5">
                {([['Élève', a.isSignedByStudent], ['Entreprise', a.isSignedByCompany], ['École', a.isSignedBySchool]] as const).map(([label, ok]) => (
                  <span key={label} className={ok ? 'text-green-700' : 'text-gray-400'}>
                    <FontAwesomeIcon icon={ok ? faCheck : faHourglass} className="mr-1" />{label}
                  </span>
                ))}
              </div>
              <div className="flex gap-2 flex-wrap mt-1">
                {isStudent && !a.isSignedByStudent && (
                  <Button design="Emphasized" disabled={signMutation.isPending}
                    onClick={() => signMutation.mutate({ id: a.id, party: 'Student' })}>
                    Signer l'avenant
                  </Button>
                )}
                {isCoord && !a.isSignedByCompany && (
                  <Button disabled={signMutation.isPending}
                    onClick={() => setCompanySigner({ id: a.id, name: internship.companySupervisorName })}>
                    Signature entreprise reçue
                  </Button>
                )}
                {isCoord && (
                  <Button design="Transparent" disabled={withdrawMutation.isPending} onClick={() => withdrawMutation.mutate(a.id)}>
                    Retirer
                  </Button>
                )}
              </div>
            </>
          )}
          {a.status === 'Retire' && a.withdrawalReason && <div className="text-gray-500">Motif du retrait : {a.withdrawalReason}</div>}
        </div>
      ))}

      {open && (
        <Dialog open headerText="Nouvel avenant à la convention"
          footer={
            <Bar endContent={
              <>
                <Button design="Transparent" onClick={() => setOpen(false)}>Annuler</Button>
                <Button design="Emphasized" disabled={!reason.trim() || createMutation.isPending} onClick={() => createMutation.mutate()}>
                  {createMutation.isPending ? 'Création...' : "Créer et signer pour l'école"}
                </Button>
              </>
            } />
          }
        >
          <div className="flex flex-col gap-3 p-4" style={{ minWidth: '420px' }}>
            {error && <div className="p-2 bg-red-50 text-red-700 rounded text-sm">{error}</div>}
            <label className="text-sm font-semibold">Motif (obligatoire)
              <TextArea rows={2} value={reason} onInput={(e: any) => setReason(e.target.value)}
                placeholder="Ex : prolongation de 4 semaines demandée par l'entreprise" style={{ width: '100%' }} />
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="text-sm font-semibold">Date de début
                <input type="date" value={startDate} disabled={(internship.statusName || internship.status) !== 'Valide'}
                  onChange={(e) => setStartDate(e.target.value)} style={inputStyle} />
              </label>
              <label className="text-sm font-semibold">Date de fin
                <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} style={inputStyle} />
              </label>
            </div>
            <label className="text-sm font-semibold">Titre du sujet
              <Input value={title} onInput={(e: any) => setTitle(e.target.value)} style={{ width: '100%' }} />
            </label>
            <label className="text-sm font-semibold">Tuteur entreprise
              <Input value={tutorName} onInput={(e: any) => setTutorName(e.target.value)} style={{ width: '100%' }} />
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="text-sm font-semibold">E-mail du tuteur
                <Input type="Email" value={tutorEmail} onInput={(e: any) => setTutorEmail(e.target.value)} style={{ width: '100%' }} />
              </label>
              <label className="text-sm font-semibold">Téléphone du tuteur
                <Input value={tutorPhone} onInput={(e: any) => setTutorPhone(e.target.value)} style={{ width: '100%' }} />
              </label>
            </div>
            <Text style={{ fontSize: '12px', color: '#666' }}>
              Seules les valeurs modifiées sont reprises. L'avenant s'applique au dossier une fois signé par l'élève et l'entreprise.
              La date de début ne peut plus changer une fois le stage démarré.
            </Text>
          </div>
        </Dialog>
      )}

      {companySigner && (
        <Dialog open headerText="Signature de l'entreprise sur l'avenant"
          footer={
            <Bar endContent={
              <>
                <Button design="Transparent" onClick={() => setCompanySigner(null)}>Annuler</Button>
                <Button design="Emphasized" disabled={!companySigner.name.trim() || signMutation.isPending}
                  onClick={() => signMutation.mutate({ id: companySigner.id, party: 'Company', signerName: companySigner.name.trim() })}>
                  Enregistrer la signature
                </Button>
              </>
            } />
          }
        >
          <div className="flex flex-col gap-3 p-4" style={{ minWidth: '380px' }}>
            <label className="text-sm font-semibold">Nom du signataire pour l'entreprise
              <Input value={companySigner.name} onInput={(e: any) => setCompanySigner({ ...companySigner, name: e.target.value })} style={{ width: '100%' }} />
            </label>
          </div>
        </Dialog>
      )}
    </div>
  );
};
