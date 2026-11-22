import React from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCalendar, faFileExcel } from '@fortawesome/free-solid-svg-icons';
import { YearlyStat } from '../../types';

interface Props {
  years: YearlyStat[];
  selected: string;
  onSelect: (year: string) => void;
  /** Absent pour les profils sans droit d'export (élève). */
  onExport?: () => void;
  exportError?: string;
}

/** Choix de l'année universitaire, bilan annuel comparatif et export Excel du périmètre de l'utilisateur. */
export const YearlyStatsCard: React.FC<Props> = ({ years, selected, onSelect, onExport, exportError }) => (
  <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5 flex flex-col gap-4">
    <div className="flex justify-between items-center flex-wrap gap-3">
      <div className="flex items-center gap-2 font-semibold text-gray-800">
        <FontAwesomeIcon icon={faCalendar} className="text-blue-600" />
        Année universitaire
        <select value={selected} onChange={(e) => onSelect(e.target.value)}
          className="ml-2 px-3 py-1.5 border border-gray-300 rounded-lg text-sm font-normal cursor-pointer">
          <option value="">Toutes les années</option>
          {years.map(y => <option key={y.academicYear} value={y.academicYear}>{y.academicYear}</option>)}
        </select>
      </div>
      {onExport && (
        <button onClick={onExport}
          className="flex items-center gap-2 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg text-sm font-semibold">
          <FontAwesomeIcon icon={faFileExcel} /> Exporter {selected ? selected : 'tout'} (Excel / CSV)
        </button>
      )}
    </div>
    {exportError && <div className="text-sm text-red-600">{exportError}</div>}

    {years.length > 0 && (
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-gray-500 border-b">
              <th className="py-2 pr-4">Année</th>
              <th className="py-2 pr-4">Dossiers</th>
              <th className="py-2 pr-4">En cours</th>
              <th className="py-2 pr-4">Terminés</th>
              <th className="py-2 pr-4">Annulés</th>
              <th className="py-2 pr-4">Note moyenne</th>
            </tr>
          </thead>
          <tbody>
            {years.map(y => (
              <tr key={y.academicYear}
                onClick={() => onSelect(y.academicYear === selected ? '' : y.academicYear)}
                className={`border-b last:border-0 cursor-pointer hover:bg-blue-50 ${y.academicYear === selected ? 'bg-blue-50 font-semibold' : ''}`}>
                <td className="py-2 pr-4">{y.academicYear}</td>
                <td className="py-2 pr-4">{y.total}</td>
                <td className="py-2 pr-4">{y.inProgress}</td>
                <td className="py-2 pr-4">{y.completed}</td>
                <td className="py-2 pr-4">{y.cancelled}</td>
                <td className="py-2 pr-4">{y.averageFinalGrade != null ? `${y.averageFinalGrade}/20` : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )}
  </div>
);
