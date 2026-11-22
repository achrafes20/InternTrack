import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bar, Button, Dialog, Input, TextArea } from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTriangleExclamation } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { useAuth } from '../../context/AuthContext';
import { CompanyRef, InternshipOffer, Track, apiErrorMessage } from '../../types';

const fieldStyle = {
  width: '100%', padding: '7px 10px', borderRadius: '6px',
  border: '1px solid #d1d5db', fontSize: '14px', boxSizing: 'border-box' as const
};

const ErrorBanner: React.FC<{ message: string }> = ({ message }) =>
  message ? (
    <div className="p-2.5 bg-red-50 text-red-700 rounded text-sm">
      <FontAwesomeIcon icon={faTriangleExclamation} className="mr-1" /> {message}
    </div>
  ) : null;

const Label: React.FC<{ children: React.ReactNode }> = ({ children }) =>
  <label className="block mb-1 font-semibold text-xs">{children}</label>;

/**
 * Publication (ou modification, si <paramref name="offer"/> est fourni) d'une offre de stage par un responsable
 * de filière (sa filière uniquement) ou un administrateur. L'entreprise et la filière d'une offre publiée sont figées.
 */
export const CreateOfferDialog: React.FC<{ onClose: () => void; offer?: InternshipOffer }> = ({ onClose, offer }) => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const isHead = user?.role === 'ResponsableFiliere';
  const isEdit = !!offer;

  const [form, setForm] = useState(() => ({
    title: offer?.title ?? '',
    description: offer?.description ?? '',
    requiredSkills: offer?.requiredSkills ?? '',
    city: offer?.city ?? '',
    durationInWeeks: offer?.durationInWeeks ?? 24,
    monthlyStipend: offer?.monthlyStipend != null ? String(offer.monthlyStipend) : '',
    expirationDate: offer ? offer.expirationDate.substring(0, 10) : new Date(Date.now() + 90 * 24 * 3600 * 1000).toISOString().substring(0, 10),
    companyId: offer?.companyId ?? '',
    academicTrackId: offer?.academicTrackId ?? (isHead ? user?.academicTrackId ?? '' : ''),
  }));
  const [error, setError] = useState('');
  const set = (key: keyof typeof form) => (value: string | number) => setForm(f => ({ ...f, [key]: value }));

  const { data: companies = [] } = useQuery<CompanyRef[]>({ queryKey: ['companies'], queryFn: async () => (await api.get('/companies')).data });
  const { data: tracks = [] } = useQuery<Track[]>({ queryKey: ['tracks'], queryFn: async () => (await api.get('/tracks')).data });

  const mutation = useMutation({
    mutationFn: async () => {
      setError('');
      const payload = {
        ...form,
        durationInWeeks: Number(form.durationInWeeks),
        monthlyStipend: form.monthlyStipend === '' ? null : Number(form.monthlyStipend),
        expirationDate: new Date(`${form.expirationDate}T23:59:59Z`).toISOString(),
      };
      if (isEdit) await api.put(`/offers/${offer!.id}`, payload);
      else await api.post('/offers', payload);
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['offers'] }); onClose(); },
    onError: (err: any) => setError(apiErrorMessage(err, "Erreur lors de l'enregistrement de l'offre.")),
  });

  const ready = form.title && form.city && form.companyId && form.academicTrackId && form.expirationDate;

  return (
    <Dialog open headerText={isEdit ? "Modifier l'offre de stage" : 'Publier une offre de stage'}
      footer={<Bar endContent={<>
        <Button design="Transparent" onClick={onClose}>Annuler</Button>
        <Button design="Emphasized" disabled={!ready || mutation.isPending} onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Enregistrement...' : isEdit ? 'Enregistrer' : "Publier l'offre"}
        </Button>
      </>} />}
    >
      <div className="flex flex-col gap-3 p-4" style={{ minWidth: '480px', maxWidth: '600px' }}>
        <ErrorBanner message={error} />
        <div>
          <Label>Intitulé :</Label>
          <Input value={form.title} onInput={(e: any) => set('title')(e.target.value)} style={{ width: '100%' }} />
        </div>
        <div>
          <Label>Description :</Label>
          <TextArea rows={3} value={form.description} onInput={(e: any) => set('description')(e.target.value)} style={{ width: '100%' }} />
        </div>
        <div>
          <Label>Compétences requises (séparées par des virgules) :</Label>
          <Input value={form.requiredSkills} onInput={(e: any) => set('requiredSkills')(e.target.value)} style={{ width: '100%' }} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <Label>Entreprise :</Label>
            <select style={fieldStyle} value={form.companyId} disabled={isEdit} onChange={(e) => set('companyId')(e.target.value)}>
              <option value="">-- Choisir --</option>
              {companies.map(c => <option key={c.id} value={c.id}>{c.name} ({c.city})</option>)}
            </select>
          </div>
          <div>
            <Label>Filière :</Label>
            <select style={fieldStyle} value={form.academicTrackId} disabled={isHead || isEdit} onChange={(e) => set('academicTrackId')(e.target.value)}>
              <option value="">-- Choisir --</option>
              {tracks.map(t => <option key={t.id} value={t.id}>{t.code} — {t.name}</option>)}
            </select>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <Label>Ville :</Label>
            <Input value={form.city} onInput={(e: any) => set('city')(e.target.value)} style={{ width: '100%' }} />
          </div>
          <div>
            <Label>Durée (semaines) :</Label>
            <input type="number" min={1} max={52} style={fieldStyle} value={form.durationInWeeks}
              onChange={(e) => set('durationInWeeks')(Number(e.target.value))} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <Label>Gratification mensuelle (MAD, optionnel) :</Label>
            <input type="number" min={0} style={fieldStyle} value={form.monthlyStipend}
              onChange={(e) => set('monthlyStipend')(e.target.value)} />
          </div>
          <div>
            <Label>Date limite de candidature :</Label>
            <input type="date" style={fieldStyle} value={form.expirationDate} onChange={(e) => set('expirationDate')(e.target.value)} />
          </div>
        </div>
      </div>
    </Dialog>
  );
};

/** Ajout (ou modification si <paramref name="company"/> est fourni) d'une entreprise partenaire (administrateur). */
export const CreateCompanyDialog: React.FC<{ onClose: () => void; company?: CompanyRef }> = ({ onClose, company }) => {
  const queryClient = useQueryClient();
  const [form, setForm] = useState({
    name: company?.name ?? '', industry: company?.industry ?? '', city: company?.city ?? '', address: company?.address ?? '',
    contactEmail: company?.contactEmail ?? '', contactPhone: company?.contactPhone ?? '', website: company?.website ?? '',
  });
  const [error, setError] = useState('');
  const set = (key: keyof typeof form) => (value: string) => setForm(f => ({ ...f, [key]: value }));

  const mutation = useMutation({
    mutationFn: async () => {
      setError('');
      const send = company ? (body: object) => api.put(`/companies/${company.id}`, body) : (body: object) => api.post('/companies', body);
      await send({
        ...form,
        contactPhone: form.contactPhone || null,
        website: form.website || null,
      });
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['companies'] }); onClose(); },
    onError: (err: any) => setError(apiErrorMessage(err, "Erreur lors de l'ajout de l'entreprise.")),
  });

  const fields: Array<[keyof typeof form, string]> = [
    ['name', 'Raison sociale'], ['industry', "Secteur d'activité"], ['city', 'Ville'], ['address', 'Adresse'],
    ['contactEmail', 'Email de contact'], ['contactPhone', 'Téléphone (optionnel)'], ['website', 'Site web (optionnel)'],
  ];

  return (
    <Dialog open headerText={company ? `Modifier : ${company.name}` : 'Ajouter une entreprise partenaire'}
      footer={<Bar endContent={<>
        <Button design="Transparent" onClick={onClose}>Annuler</Button>
        <Button design="Emphasized" disabled={!form.name || !form.city || !form.contactEmail || mutation.isPending}
          onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Enregistrement...' : company ? 'Enregistrer' : 'Ajouter'}
        </Button>
      </>} />}
    >
      <div className="flex flex-col gap-3 p-4" style={{ minWidth: '420px' }}>
        <ErrorBanner message={error} />
        {fields.map(([key, label]) => (
          <div key={key}>
            <Label>{label} :</Label>
            <Input value={form[key]} onInput={(e: any) => set(key)(e.target.value)} style={{ width: '100%' }} />
          </div>
        ))}
      </div>
    </Dialog>
  );
};

/** Liste des entreprises partenaires avec modification (administrateur). */
export const CompaniesDialog: React.FC<{ onClose: () => void }> = ({ onClose }) => {
  const [editing, setEditing] = useState<CompanyRef | null>(null);
  const [creating, setCreating] = useState(false);
  const { data: companies = [] } = useQuery<CompanyRef[]>({ queryKey: ['companies'], queryFn: async () => (await api.get('/companies')).data });

  if (editing) return <CreateCompanyDialog company={editing} onClose={() => setEditing(null)} />;
  if (creating) return <CreateCompanyDialog onClose={() => setCreating(false)} />;

  return (
    <Dialog open headerText={`Entreprises partenaires (${companies.length})`}
      footer={<Bar endContent={<>
        <Button onClick={() => setCreating(true)}>Ajouter une entreprise</Button>
        <Button design="Emphasized" onClick={onClose}>Fermer</Button>
      </>} />}
    >
      <div className="flex flex-col gap-2 p-4" style={{ minWidth: '480px', maxHeight: '60vh', overflowY: 'auto' }}>
        {companies.map(c => (
          <div key={c.id} className="flex justify-between items-center gap-3 p-2.5 border border-gray-200 rounded-lg">
            <div className="min-w-0">
              <div className="font-semibold text-sm truncate">{c.name}</div>
              <div className="text-xs text-gray-500 truncate">{c.city}{c.contactEmail ? ` · ${c.contactEmail}` : ''}</div>
            </div>
            <Button design="Transparent" onClick={() => setEditing(c)}>Modifier</Button>
          </div>
        ))}
      </div>
    </Dialog>
  );
};
