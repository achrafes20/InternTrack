import React from 'react';
import { Tag } from '@ui5/webcomponents-react';

interface StatusBadgeProps {
  status: string;
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({ status }) => {
  switch (status) {
    case 'Brouillon':
      return <Tag colorScheme="6">Brouillon</Tag>;
    case 'Propose':
      return <Tag colorScheme="1">Proposé</Tag>;
    case 'Valide':
      return <Tag colorScheme="8">Validé</Tag>;
    case 'Refuse':
      return <Tag colorScheme="2">Refusé</Tag>;
    case 'EnCours':
      return <Tag colorScheme="5">En cours</Tag>;
    case 'SoutenancePlanifiee':
      return <Tag colorScheme="9">Soutenance planifiée</Tag>;
    case 'Termine':
      return <Tag colorScheme="10">Terminé</Tag>;
    default:
      return <Tag colorScheme="7">{status}</Tag>;
  }
};
