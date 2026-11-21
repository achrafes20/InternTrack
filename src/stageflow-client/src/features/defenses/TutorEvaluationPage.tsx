import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faStar } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { apiErrorMessage } from '../../types';
import { PublicCard, Alert } from '../auth/PasswordPages';

interface TutorEvaluationInfo {
  studentFullName: string;
  internshipTitle: string;
  companyName: string;
  tutorName: string;
  scheduledAt: string;
  scheduledAtLabel: string;
  classroom: string;
  hasStarted: boolean;
  currentGrade?: number;
  currentComment?: string;
  linkExpiresAt: string;
}

/** Page publique ouverte par le tuteur entreprise (membre externe du jury) depuis le lien reçu par e-mail. */
export const TutorEvaluationPage: React.FC = () => {
  const { token = '' } = useParams<{ token: string }>();
  const [grade, setGrade] = useState('');
  const [comment, setComment] = useState('');

  const { data, isLoading, error, refetch } = useQuery<TutorEvaluationInfo>({
    queryKey: ['tutor-evaluation', token],
    queryFn: async () => (await api.get(`/defenses/tutor-evaluation/${encodeURIComponent(token)}`)).data,
    retry: false,
  });

  useEffect(() => {
    if (data?.currentGrade !== undefined && data.currentGrade !== null) setGrade(String(data.currentGrade));
    if (data?.currentComment) setComment(data.currentComment);
  }, [data]);

  const submit = useMutation({
    mutationFn: async () => {
      await api.put(`/defenses/tutor-evaluation/${encodeURIComponent(token)}`, {
        grade: Number(grade.replace(',', '.')),
        comment: comment || null,
      });
    },
    onSuccess: () => refetch(),
  });

  if (isLoading) return <PublicCard title="Évaluation de soutenance">Chargement...</PublicCard>;

  if (error || !data) {
    return (
      <PublicCard title="Lien d'évaluation indisponible">
        <Alert kind="error">{apiErrorMessage(error, "Ce lien est invalide, a expiré, ou la note finale a déjà été arrêtée par le jury.")}</Alert>
      </PublicCard>
    );
  }

  return (
    <PublicCard title="Évaluation de la soutenance" subtitle={`${data.tutorName} — ${data.companyName}`}>
      <dl className="text-sm grid grid-cols-[auto,1fr] gap-x-3 gap-y-1.5 mb-4">
        <dt className="text-gray-500">Élève</dt><dd className="font-semibold">{data.studentFullName}</dd>
        <dt className="text-gray-500">Stage</dt><dd className="font-semibold">{data.internshipTitle}</dd>
        <dt className="text-gray-500">Soutenance</dt><dd>{data.scheduledAtLabel}, salle {data.classroom}</dd>
      </dl>

      {!data.hasStarted ? (
        <Alert kind="error">L'évaluation sera possible à partir du début de la soutenance ({data.scheduledAtLabel}).</Alert>
      ) : (
        <form onSubmit={(e) => { e.preventDefault(); submit.mutate(); }} className="flex flex-col gap-3">
          {submit.isError && <Alert kind="error">{apiErrorMessage(submit.error, "L'enregistrement a échoué.")}</Alert>}
          {submit.isSuccess && <Alert kind="ok">Évaluation enregistrée. Vous pouvez la modifier jusqu'à l'arrêt de la note finale.</Alert>}
          <label className="text-sm font-semibold text-gray-700">Note proposée (sur 20)
            <input type="number" min={0} max={20} step={0.25} required value={grade} onChange={(e) => setGrade(e.target.value)}
              className="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm mt-1 box-border" />
          </label>
          <label className="text-sm font-semibold text-gray-700">Appréciation (optionnel)
            <textarea rows={4} value={comment} onChange={(e) => setComment(e.target.value)} maxLength={2000}
              className="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm mt-1 box-border"
              placeholder="Qualité du travail réalisé en entreprise, autonomie, intégration dans l'équipe..." />
          </label>
          <button type="submit" disabled={grade === '' || submit.isPending}
            className="py-2.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-60 text-white rounded-lg font-semibold text-sm">
            <FontAwesomeIcon icon={faStar} className="mr-2" />
            {submit.isPending ? 'Enregistrement...' : data.currentGrade !== undefined && data.currentGrade !== null ? 'Mettre à jour mon évaluation' : 'Enregistrer mon évaluation'}
          </button>
          <p className="text-xs text-gray-400">
            Votre évaluation est transmise au jury et n'est pas communiquée à l'élève. Lien valable jusqu'au {new Date(data.linkExpiresAt).toLocaleDateString('fr-FR')}.
          </p>
        </form>
      )}
    </PublicCard>
  );
};
