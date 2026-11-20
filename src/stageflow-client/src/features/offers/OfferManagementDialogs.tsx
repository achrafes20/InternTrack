import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bar, Button, Dialog, Input, TextArea } from '@ui5/webcomponents-react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTriangleExclamation } from '@fortawesome/free-solid-svg-icons';
import { api } from '../../api/client';
import { useAuth } from '../../context/AuthContext';
import { CompanyRef, Track, apiErrorMessage } from '../../types';

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

/** Publication d'une offre de stage par un responsable de filière (sa filière uniquement) ou un administrateur. */
export const CreateOfferDialog: React.FC<{ onClose: () => void }> = ({ onClose }) => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const isHead = user?.role === 'ResponsableFiliere';

  const inThreeMonths = new Date(Date.now() + 90 * 24 * 3600 * 1000).toISOString().substring(0, 10);
  const [form, setForm] = useState({
    title: '', description: '', requiredSkills: '', city: '',
    durationInWeeks: 24, monthlyStipend: '', expirationDate: inThreeMonths,
    companyId: '', academicTrackId: isHead ? user?.academicTrackId ?? '' : '',
  });
  const [error, setError] = useState('');
  const set = (key: keyof typeof form) => (value: string | number) => setForm(f => ({ ...f, [key]: value }));

  const { data: companies = [] } = useQuery<CompanyRef[]>({ queryKey: ['companies'], queryFn: async () => (await api.get('/companies')).data });
  const { data: tracks = [] } = useQuery<Track[]>({ queryKey: ['tracks'], queryFn: async () => (await api.get('/tracks')).data });

  const mutation = useMutation({
    mutationFn: async () => {
      setError('');
      await api.post('/offers', {
        ...form,
        durationInWeeks: Number(form.durationInWeeks),
        monthlyStipend: form.monthlyStipend === '' ? null : Number(form.monthlyStipend),
        expirationDate: new Date(`${form.expirationDate}T23:59:59Z`).toISOString(),
      });
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['offers'] }); onClose(); },
    onError: (err: any) => setError(apiErrorMessage(err, "Erreur lors de la publication de l'offre.")),
  });

  const ready = form.title && form.city && form.companyId && form.academicTrackId && form.expirationDate;

  return (
    <Dialog open headerText="Publier une offre de stage"
      footer={<Bar endContent={<>
        <Button design="Transparent" onClick={onClose}>Annuler</Button>
        <Button design="Emphasized" disabled={!ready || mutation.isPending} onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Publication...' : "Publier l'offre"}
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
            <select style={fieldStyle} value={form.companyId} onChange={(e) => set('companyId')(e.target.value)}>
              <option value="">-- Choisir --</option>
              {companies.map(c => <option key={c.id} value={c.id}>{c.name} ({c.city})</option>)}
            </select>
          </div>
          <div>
            <Label>Filière :</Label>
            <select style={fieldStyle} value={form.academicTrackId} disabled={isHead} onChange={(e) => set('academicTrackId')(e.target.value)}>
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

/** Ajout d'une entreprise partenaire (administrateur). */
export const CreateCompanyDialog: React.FC<{ onClose: () => void }> = ({ onClose }) => {
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ name: '', industry: '', city: '', address: '', contactEmail: '', contactPhone: '', website: '' });
  const [error, setError] = useState('');
  const set = (key: keyof typeof form) => (value: string) => setForm(f => ({ ...f, [key]: value }));

  const mutation = useMutation({
    mutationFn: async () => {
      setError('');
      await api.post('/companies', {
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
    <Dialog open headerText="Ajouter une entreprise partenaire"
      footer={<Bar endContent={<>
        <Button design="Transparent" onClick={onClose}>Annuler</Button>
        <Button design="Emphasized" disabled={!form.name || !form.city || !form.contactEmail || mutation.isPending}
          onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Enregistrement...' : 'Ajouter'}
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
