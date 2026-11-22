import React, { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { BusyIndicator, Title, Text } from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTriangleExclamation, faCircleExclamation, faCircleCheck, faArrowRight, faRotate } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { Anomaly, apiErrorMessage } from '../../types';

/**
 * Rapport d'anomalies : incohérences des données (souvent antérieures aux règles actuelles).
 * Chaque ligne indique la correction à apporter et renvoie vers le dossier concerné.
 */
export const AnomaliesPage: React.FC = () => {
  const navigate = useNavigate();
  const [severity, setSeverity] = useState<'all' | 'Error' | 'Warning'>('all');

  const { data: anomalies = [], isLoading, error, refetch, isFetching } = useQuery<Anomaly[]>({
    queryKey: ['anomalies'],
    queryFn: async () => (await api.get('/anomalies')).data,
  });

  const filtered = useMemo(
    () => anomalies.filter(a => severity === 'all' || a.severity === severity),
    [anomalies, severity]);

  const errors = anomalies.filter(a => a.severity === 'Error').length;
  const warnings = anomalies.length - errors;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex justify-between items-start flex-wrap gap-3">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <FontAwesomeIcon icon={faTriangleExclamation} className="text-amber-500 text-xl" />
            <Title level="H2">Rapport d'anomalies</Title>
          </div>
          <Text style={{ color: '#555' }}>
            Données incohérentes avec les règles de gestion (souvent antérieures à leur mise en place), avec la correction à apporter.
          </Text>
        </div>
        <button onClick={() => refetch()} disabled={isFetching}
          className="flex items-center gap-2 px-4 py-2 border border-gray-300 hover:bg-gray-50 rounded-lg text-sm font-semibold">
          <FontAwesomeIcon icon={faRotate} spin={isFetching} /> Relancer l'analyse
        </button>
      </div>

      <div className="flex gap-2 flex-wrap">
        {([['all', `Tout (${anomalies.length})`], ['Error', `À corriger (${errors})`], ['Warning', `À vérifier (${warnings})`]] as const).map(([value, label]) => (
          <button key={value} onClick={() => setSeverity(value)}
            className={`px-3 py-1.5 rounded-full text-xs font-semibold border ${severity === value ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-gray-700 border-gray-300'}`}>
            {label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="flex justify-center py-12"><BusyIndicator active /></div>
      ) : error ? (
        <div className="p-3 bg-red-50 text-red-700 rounded text-sm">{apiErrorMessage(error, 'Analyse impossible.')}</div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-14 bg-white rounded-2xl border border-gray-200">
          <FontAwesomeIcon icon={faCircleCheck} className="text-5xl text-green-500 mb-3" />
          <div className="font-semibold text-gray-800">Aucune anomalie détectée</div>
          <div className="text-sm text-gray-500">Les données respectent les règles de gestion.</div>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {filtered.map((a, idx) => {
            const isError = a.severity === 'Error';
            return (
              <div key={`${a.code}-${a.internshipId ?? a.userId}-${idx}`}
                className={`bg-white rounded-xl border-l-4 ${isError ? 'border-red-500' : 'border-amber-400'} border border-gray-200 p-4`}>
                <div className="flex justify-between items-start gap-3 flex-wrap">
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 font-semibold text-gray-900">
                      <FontAwesomeIcon icon={isError ? faCircleExclamation : faTriangleExclamation}
                        className={isError ? 'text-red-500' : 'text-amber-500'} />
                      {a.title}
                      <span className="text-[10px] font-mono text-gray-400">{a.code}</span>
                    </div>
                    <div className="text-sm text-gray-700 mt-1">{a.detail}</div>
                    <div className="text-xs text-blue-800 bg-blue-50 rounded px-2 py-1 mt-2 inline-block">
                      Correction : {a.resolution}
                    </div>
                  </div>
                  {a.internshipId ? (
                    <button onClick={() => navigate('/internships', { state: { focusInternshipId: a.internshipId } })}
                      className="flex items-center gap-1.5 px-3 py-1.5 border border-gray-300 hover:bg-gray-50 rounded-lg text-xs font-semibold whitespace-nowrap">
                      Ouvrir le dossier <FontAwesomeIcon icon={faArrowRight} />
                    </button>
                  ) : a.userId ? (
                    <button onClick={() => navigate('/users')}
                      className="flex items-center gap-1.5 px-3 py-1.5 border border-gray-300 hover:bg-gray-50 rounded-lg text-xs font-semibold whitespace-nowrap">
                      Gérer le compte <FontAwesomeIcon icon={faArrowRight} />
                    </button>
                  ) : null}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
