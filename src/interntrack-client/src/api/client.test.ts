import axios, { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from 'axios';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, downloadFile, getAccessToken, refreshSession, setAccessToken, uploadFile } from './client';

const okResponse = (config: InternalAxiosRequestConfig, data: unknown = {}, headers: Record<string, string> = {}) => ({
  data, status: 200, statusText: 'OK', headers, config,
});

const unauthorized = (config: InternalAxiosRequestConfig) => {
  const response = { data: {}, status: 401, statusText: 'Unauthorized', headers: {}, config };
  return new AxiosError('401', 'ERR_BAD_REQUEST', config, null, response as never);
};

const refreshPayload = (token: string) => ({ data: { accessToken: token, accessTokenExpiresAt: '2030-01-01T00:00:00Z', user: {} } });

describe('client API', () => {
  const originalAdapter = api.defaults.adapter;

  beforeEach(() => {
    setAccessToken(null);
  });

  afterEach(() => {
    api.defaults.adapter = originalAdapter;
    vi.unstubAllGlobals();
  });

  it('ajoute le jeton d\'accès en mémoire à chaque requête', async () => {
    const seen: string[] = [];
    api.defaults.adapter = (async (config) => {
      seen.push(String(config.headers.Authorization));
      return okResponse(config);
    }) as AxiosAdapter;

    setAccessToken('jeton-1');
    await api.get('/internships');

    expect(seen).toEqual(['Bearer jeton-1']);
  });

  it('sérialise le renouvellement entre onglets avec Web Locks et mémorise le nouveau jeton', async () => {
    const request = vi.fn(async (_name: string, callback: () => Promise<unknown>) => callback());
    vi.stubGlobal('navigator', { ...navigator, locks: { request } });
    const post = vi.spyOn(axios, 'post').mockResolvedValue(refreshPayload('jeton-renouvele'));

    const data = await refreshSession();

    expect(request).toHaveBeenCalledWith('interntrack-session-refresh', expect.any(Function));
    expect(post).toHaveBeenCalledWith(expect.stringMatching(/\/api\/v1\/auth\/refresh$/), {}, { withCredentials: true });
    expect(data.accessToken).toBe('jeton-renouvele');
    expect(getAccessToken()).toBe('jeton-renouvele');
  });

  it('fonctionne sans Web Locks (navigateurs anciens)', async () => {
    vi.stubGlobal('navigator', { ...navigator, locks: undefined });
    vi.spyOn(axios, 'post').mockResolvedValue(refreshPayload('jeton-sans-lock'));

    await refreshSession();

    expect(getAccessToken()).toBe('jeton-sans-lock');
  });

  it('sur 401, ne renouvelle la session qu\'une fois pour des requêtes simultanées puis les rejoue', async () => {
    vi.stubGlobal('navigator', { ...navigator, locks: undefined });
    let releaseRefresh!: () => void;
    const post = vi.spyOn(axios, 'post').mockImplementation(
      () => new Promise((resolve) => { releaseRefresh = () => resolve(refreshPayload('nouveau')); }),
    );

    api.defaults.adapter = (async (config) => {
      if (config.headers.Authorization === 'Bearer nouveau') return okResponse(config, { url: config.url });
      throw unauthorized(config);
    }) as AxiosAdapter;

    setAccessToken('expire');
    const first = api.get('/internships');
    const second = api.get('/defenses');
    await vi.waitFor(() => expect(post).toHaveBeenCalledTimes(1));
    releaseRefresh();

    const [a, b] = await Promise.all([first, second]);
    expect(post).toHaveBeenCalledTimes(1);
    expect(a.data.url).toBe('/internships');
    expect(b.data.url).toBe('/defenses');
  });

  it('vide le jeton et rejette si le renouvellement échoue', async () => {
    vi.stubGlobal('navigator', { ...navigator, locks: undefined });
    vi.spyOn(axios, 'post').mockRejectedValue(new Error('session expirée'));
    api.defaults.adapter = (async (config) => { throw unauthorized(config); }) as AxiosAdapter;

    setAccessToken('expire');

    await expect(api.get('/internships')).rejects.toThrow('session expirée');
    expect(getAccessToken()).toBeNull();
  });

  it('ne tente pas de renouvellement pour l\'appel de connexion', async () => {
    const post = vi.spyOn(axios, 'post');
    api.defaults.adapter = (async (config) => { throw unauthorized(config); }) as AxiosAdapter;

    await expect(api.post('/auth/login', {})).rejects.toBeInstanceOf(AxiosError);
    expect(post).not.toHaveBeenCalled();
  });

  it('télécharge un fichier avec le nom fourni par Content-Disposition (UTF-8)', async () => {
    api.defaults.adapter = (async (config) =>
      okResponse(config, new Blob(['%PDF-1.7']), {
        'content-disposition': "attachment; filename=\"memoire.pdf\"; filename*=UTF-8''m%C3%A9moire%20final.pdf",
      })) as AxiosAdapter;

    const createObjectURL = vi.fn(() => 'blob:fichier');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    const clicked: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push(this);
    });

    await downloadFile('/documents/1/download', 'document.pdf');

    expect(clicked).toHaveLength(1);
    expect(clicked[0].getAttribute('download')).toBe('mémoire final.pdf');
    expect(clicked[0].href).toBe('blob:fichier');
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:fichier');
    expect(document.querySelector('a[download]')).toBeNull();
  });

  it('utilise le nom de repli sans Content-Disposition', async () => {
    api.defaults.adapter = (async (config) => okResponse(config, new Blob(['x']))) as AxiosAdapter;
    vi.stubGlobal('URL', { ...URL, createObjectURL: () => 'blob:x', revokeObjectURL: () => undefined });
    const clicked: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push(this);
    });

    await downloadFile('/dashboard/export', 'export.csv');

    expect(clicked[0].getAttribute('download')).toBe('export.csv');
  });

  it('envoie le fichier déposé dans le champ « file » en multipart', async () => {
    let body: unknown;
    api.defaults.adapter = (async (config) => { body = config.data; return okResponse(config); }) as AxiosAdapter;

    const file = new File(['%PDF-1.7'], 'memoire.pdf', { type: 'application/pdf' });
    await uploadFile('/internships/1/documents/thesis', file);

    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get('file')).toBeInstanceOf(File);
  });
});
