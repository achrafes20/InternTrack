import React from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Card, CardHeader, Title, Text, Tag } from '@ui5/webcomponents-react';
import { api } from '../../api/client';

export const VerifyConventionPage: React.FC = () => {
  const { token } = useParams<{ token: string }>();

  const { data, isLoading, error } = useQuery({
    queryKey: ['verify-convention', token],
    queryFn: async () => {
      const res = await api.get(`/conventions/verify/${token}`);
      return res.data;
    },
    enabled: !!token,
  });

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', background: '#f0f2f5', padding: '20px' }}>
      <Card style={{ maxWidth: '600px', width: '100%', padding: '24px', boxShadow: '0 10px 30px rgba(0,0,0,0.08)' }}>
        <CardHeader
          titleText="Vérification Officielle d'Authenticité"
          subtitleText="Système d'Accréditation ENSA - StageFlow"
        />

        {isLoading && <div style={{ padding: '20px', textAlign: 'center' }}>Vérification de l'empreinte cryptographique en cours...</div>}

        {error && (
          <div style={{ padding: '20px', textAlign: 'center', color: '#d32f2f' }}>
            <Title level="H3" style={{ color: '#d32f2f' }}>DOCUMENT NON CERTIFIÉ</Title>
            <Text>Ce jeton de vérification est invalide ou inexistant. Le document ne provient pas du serveur officiel ENSA.</Text>
          </div>
        )}

        {data && (
          <div style={{ marginTop: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <div style={{ textAlign: 'center', padding: '12px', background: '#e8f5e9', borderRadius: '6px' }}>
              <Tag colorScheme="8" style={{ fontSize: '14px', padding: '6px 12px' }}>CONVENTION CERTIFIÉE & AUTHENTIQUE</Tag>
            </div>

            <div>
              <Text style={{ fontSize: '12px', color: '#666' }}>RÉFÉRENCE OFFICIELLE</Text>
              <Text style={{ fontSize: '16px', fontWeight: 'bold' }}>{data.conventionNumber}</Text>
            </div>

            <div>
              <Text style={{ fontSize: '12px', color: '#666' }}>ÉLÈVE INGÉNIEUR</Text>
              <Text style={{ fontSize: '15px', fontWeight: 'bold' }}>{data.studentFullName}</Text>
              <Text style={{ fontSize: '13px' }}>Filière : {data.academicTrack}</Text>
            </div>

            <div>
              <Text style={{ fontSize: '12px', color: '#666' }}>ORGANISME D'ACCUEIL</Text>
              <Text style={{ fontSize: '15px', fontWeight: 'bold' }}>{data.companyName}</Text>
            </div>

            <div>
              <Text style={{ fontSize: '12px', color: '#666' }}>PÉRIODE VALIDÉE</Text>
              <Text style={{ fontSize: '14px' }}>
                Du {new Date(data.startDate).toLocaleDateString()} au {new Date(data.endDate).toLocaleDateString()}
              </Text>
            </div>

            <div style={{ marginTop: '12px', paddingTop: '12px', borderTop: '1px solid #eee', fontSize: '11px', color: '#888', textAlign: 'center' }}>
              Signature cryptographique validée par la Direction des Études ENSA.
            </div>
          </div>
        )}
      </Card>
    </div>
  );
};
