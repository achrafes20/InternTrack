import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Card, CardHeader, Title, Text, Tag } from '@ui5/webcomponents-react';
import { api } from '../../api/client';

interface ConventionVerification {
  conventionNumber: string;
  studentFullName: string;
  academicTrack: string;
  companyName: string;
  startDate: string;
  endDate: string;
  isAuthentic: boolean;
  isSignedByStudent: boolean;
  isSignedByCompany: boolean;
  isSignedBySchool: boolean;
  isFullySigned: boolean;
  signedAt?: string;
  signedDocumentSha256?: string;
  terminatedAt?: string;
  appliedAmendments?: number;
}

/** Empreinte SHA-256 calculée localement : le fichier ne quitte jamais l'appareil. */
const sha256Hex = async (file: File) => {
  const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer());
  return Array.from(new Uint8Array(digest)).map(b => b.toString(16).padStart(2, '0')).join('');
};

const FingerprintCheck: React.FC<{ expected: string }> = ({ expected }) => {
  const [result, setResult] = useState<'match' | 'mismatch' | null>(null);
  const [busy, setBusy] = useState(false);

  const onFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    try { setResult((await sha256Hex(file)) === expected ? 'match' : 'mismatch'); }
    finally { setBusy(false); }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
      <Text style={{ fontSize: '12px', color: '#666' }}>EMPREINTE DE L'EXEMPLAIRE SIGNÉ (SHA-256)</Text>
      <code style={{ fontSize: '11px', wordBreak: 'break-all', background: '#f5f5f5', padding: '6px', borderRadius: '4px' }}>{expected}</code>
      <label style={{ fontSize: '13px' }}>
        Vérifier un PDF reçu (calcul fait sur votre appareil, le fichier n'est pas envoyé) :
        <input type="file" accept="application/pdf,.pdf" onChange={onFile} style={{ display: 'block', marginTop: '4px' }} />
      </label>
      {busy && <Text>Calcul en cours...</Text>}
      {result === 'match' && <Text style={{ color: '#107e3e', fontWeight: 'bold' }}>✔ Le document est identique à l'exemplaire signé enregistré.</Text>}
      {result === 'mismatch' && <Text style={{ color: '#d32f2f', fontWeight: 'bold' }}>✘ Ce document diffère de l'exemplaire signé : il a été modifié ou n'est pas la version signée.</Text>}
    </div>
  );
};

const SignatureLine: React.FC<{ label: string; signed: boolean }> = ({ label, signed }) => (
  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px' }}>
    <span>{label}</span>
    <strong style={{ color: signed ? '#107e3e' : '#b45309' }}>{signed ? '✔ Signée' : 'En attente'}</strong>
  </div>
);

export const VerifyConventionPage: React.FC = () => {
  const { token } = useParams<{ token: string }>();

  const { data, isLoading, error } = useQuery<ConventionVerification>({
    queryKey: ['verify-convention', token],
    queryFn: async () => (await api.get(`/conventions/verify/${encodeURIComponent(token ?? '')}`)).data,
    enabled: !!token,
    retry: false,
  });

  const fmt = (d: string) => new Date(d).toLocaleDateString('fr-FR');

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', background: '#f0f2f5', padding: '20px' }}>
      <Card style={{ maxWidth: '600px', width: '100%', padding: '24px', boxShadow: '0 10px 30px rgba(0,0,0,0.08)' }}>
        <CardHeader
          titleText="Vérification d'une convention de stage"
          subtitleText="Portail public ENSA - InternTrack"
        />

        {isLoading && <div style={{ padding: '20px', textAlign: 'center' }}>Vérification en cours...</div>}

        {error && (
          <div style={{ padding: '20px', textAlign: 'center', color: '#d32f2f' }}>
            <Title level="H3" style={{ color: '#d32f2f' }}>CONVENTION INTROUVABLE</Title>
            <Text>Ce lien de vérification ne correspond à aucune convention enregistrée par l'ENSA. Le document présenté n'est pas authentique.</Text>
          </div>
        )}

        {data && (
          <div style={{ marginTop: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <div style={{ textAlign: 'center', padding: '12px', background: data.isFullySigned ? '#e8f5e9' : '#fff7ed', borderRadius: '6px' }}>
              <Tag colorScheme={data.isFullySigned ? '8' : '1'} style={{ fontSize: '14px', padding: '6px 12px' }}>
                {data.isFullySigned ? 'CONVENTION ENREGISTRÉE ET SIGNÉE' : 'CONVENTION ENREGISTRÉE — SIGNATURES INCOMPLÈTES'}
              </Tag>
            </div>

            {data.terminatedAt && (
              <div style={{ textAlign: 'center', padding: '10px', background: '#fdecea', color: '#b71c1c', borderRadius: '6px', fontWeight: 600 }}>
                Convention résiliée le {new Date(data.terminatedAt).toLocaleDateString('fr-FR')} : elle ne produit plus d'effet.
              </div>
            )}
            {!!data.appliedAmendments && (
              <div style={{ textAlign: 'center', padding: '8px', background: '#eef2ff', color: '#3730a3', borderRadius: '6px', fontSize: '13px' }}>
                Modifiée par {data.appliedAmendments} avenant(s) : les dates affichées ci-dessous tiennent compte des avenants
                et peuvent différer de l'exemplaire initial.
              </div>
            )}

            <div>
              <Text style={{ fontSize: '12px', color: '#666' }}>RÉFÉRENCE</Text>
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
              <Text style={{ fontSize: '12px', color: '#666' }}>PÉRIODE</Text>
              <Text style={{ fontSize: '14px' }}>Du {fmt(data.startDate)} au {fmt(data.endDate)}</Text>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              <Text style={{ fontSize: '12px', color: '#666' }}>SIGNATURES</Text>
              <SignatureLine label="Élève ingénieur" signed={data.isSignedByStudent} />
              <SignatureLine label="Entreprise d'accueil" signed={data.isSignedByCompany} />
              <SignatureLine label="École (ENSA)" signed={data.isSignedBySchool} />
              {data.signedAt && (
                <Text style={{ fontSize: '12px', color: '#107e3e' }}>Convention complète depuis le {fmt(data.signedAt)}.</Text>
              )}
            </div>

            {data.signedDocumentSha256 && <FingerprintCheck expected={data.signedDocumentSha256} />}

            <div style={{ marginTop: '12px', paddingTop: '12px', borderTop: '1px solid #eee', fontSize: '11px', color: '#888', textAlign: 'center' }}>
              Comparez ces informations avec le document présenté : en cas de différence, seules les données de ce portail font foi.
            </div>
          </div>
        )}
      </Card>
    </div>
  );
};
