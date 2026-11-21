import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSignature } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { apiErrorMessage } from '../../types';
import { PublicCard, Alert } from '../auth/PasswordPages';

interface CompanySigningInfo {
  conventionNumber: string;
  internshipTitle: string;
  subjectDescription: string;
  studentFullName: string;
  academicTrack: string;
  companyName: string;
  companySupervisorName: string;
  startDate: string;
  endDate: string;
  linkExpiresAt: string;
}

/** Page publique ouverte depuis le lien reçu par le tuteur entreprise : consultation et signature de la convention. */
export const CompanySignPage: React.FC = () => {
  const { token = '' } = useParams<{ token: string }>();
  const [signerName, setSignerName] = useState('');
  const [accepted, setAccepted] = useState(false);

  const { data, isLoading, error } = useQuery<CompanySigningInfo>({
    queryKey: ['company-signing', token],
    queryFn: async () => (await api.get(`/conventions/company-signing/${encodeURIComponent(token)}`)).data,
    retry: false,
  });

  const sign = useMutation({
    mutationFn: async () => { await api.post(`/conventions/company-signing/${encodeURIComponent(token)}`, { signerName, acceptTerms: accepted }); },
  });

  const fmt = (d: string) => new Date(d).toLocaleDateString('fr-FR');

  if (isLoading) return <PublicCard title="Convention de stage">Chargement...</PublicCard>;

  if (error || !data) {
    return (
      <PublicCard title="Lien de signature indisponible">
        <Alert kind="error">{apiErrorMessage(error, "Ce lien est invalide, a expiré ou a déjà été utilisé. Contactez l'école pour en recevoir un nouveau.")}</Alert>
      </PublicCard>
    );
  }

  if (sign.isSuccess) {
    return (
      <PublicCard title="Convention signée">
        <Alert kind="ok">Merci. La signature de {data.companyName} a été enregistrée pour la convention {data.conventionNumber}.</Alert>
        <p className="text-sm text-gray-600">L'école et l'élève sont informés via la plateforme. Vous pouvez fermer cette page.</p>
      </PublicCard>
    );
  }

  return (
    <PublicCard title="Signature de la convention de stage" subtitle={`Réf. ${data.conventionNumber}`}>
      {sign.isError && <Alert kind="error">{apiErrorMessage(sign.error, 'La signature a échoué.')}</Alert>}

      <dl className="text-sm grid grid-cols-[auto,1fr] gap-x-3 gap-y-1.5 mb-4">
        <dt className="text-gray-500">Entreprise</dt><dd className="font-semibold">{data.companyName}</dd>
        <dt className="text-gray-500">Élève</dt><dd className="font-semibold">{data.studentFullName} ({data.academicTrack})</dd>
        <dt className="text-gray-500">Stage</dt><dd className="font-semibold">{data.internshipTitle}</dd>
        <dt className="text-gray-500">Période</dt><dd>Du {fmt(data.startDate)} au {fmt(data.endDate)}</dd>
        <dt className="text-gray-500">Tuteur</dt><dd>{data.companySupervisorName}</dd>
      </dl>
      <p className="text-xs text-gray-600 bg-slate-50 rounded-lg p-3 mb-4 max-h-32 overflow-y-auto whitespace-pre-wrap">{data.subjectDescription}</p>

      <form onSubmit={(e) => { e.preventDefault(); sign.mutate(); }} className="flex flex-col gap-3">
        <label className="text-sm font-semibold text-gray-700">Nom et fonction du signataire
          <input value={signerName} onChange={(e) => setSignerName(e.target.value)} required maxLength={150}
            placeholder="Ex: Mme Idrissi, Directrice des Ressources Humaines"
            className="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm mt-1 box-border" />
        </label>
        <label className="flex items-start gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={accepted} onChange={(e) => setAccepted(e.target.checked)} className="mt-1" />
          <span>Je suis habilité(e) à engager l'entreprise et j'accepte les termes de cette convention de stage.</span>
        </label>
        <button type="submit" disabled={!signerName.trim() || !accepted || sign.isPending}
          className="py-2.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-60 text-white rounded-lg font-semibold text-sm">
          <FontAwesomeIcon icon={faSignature} className="mr-2" />{sign.isPending ? 'Signature...' : 'Signer au nom de l\'entreprise'}
        </button>
        <p className="text-xs text-gray-400">Lien personnel valable jusqu'au {fmt(data.linkExpiresAt)}, utilisable une seule fois.</p>
      </form>
    </PublicCard>
  );
};
