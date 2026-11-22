import axios from 'axios';

let inMemoryToken: string | null = null;

export const setAccessToken = (token: string | null) => {
  inMemoryToken = token;
};

export const getAccessToken = () => inMemoryToken;

// En local et sous Docker, l'API est servie sous le même domaine (/api via le proxy Vite ou nginx).
// En déploiement séparé (ex. Azure Static Web Apps + App Service), VITE_API_BASE_URL pointe vers l'API.
const API_ORIGIN = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? '';
const API_BASE_URL = `${API_ORIGIN}/api/v1`;

export const api = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const apiClient = api;

api.interceptors.request.use((config) => {
  if (inMemoryToken && config.headers) {
    config.headers.Authorization = `Bearer ${inMemoryToken}`;
  }
  return config;
});

export interface RefreshResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: any;
}

/**
 * Renouvelle la session via le cookie HttpOnly.
 * Les rafraîchissements sont sérialisés entre tous les onglets (Web Locks API) : le refresh token tourne
 * à chaque utilisation, et deux onglets qui le présenteraient en même temps seraient pris pour un rejeu.
 * Comme le cookie est partagé, l'onglet qui attend envoie le cookie déjà renouvelé par le premier.
 * Le serveur tolère en plus une courte période de réutilisation pour les navigateurs sans Web Locks.
 */
export const refreshSession = async (): Promise<RefreshResponse> => {
  const doRefresh = async () =>
    (await axios.post<RefreshResponse>(`${API_BASE_URL}/auth/refresh`, {}, { withCredentials: true })).data;

  const data = typeof navigator !== 'undefined' && navigator.locks
    ? await navigator.locks.request('interntrack-session-refresh', doRefresh)
    : await doRefresh();

  setAccessToken(data.accessToken);
  return data;
};

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: any) => void;
}> = [];

const processQueue = (error: any, token: string | null = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token!);
    }
  });
  failedQueue = [];
};

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry && originalRequest.url !== '/auth/login' && originalRequest.url !== '/auth/refresh') {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return api(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const data = await refreshSession();
        processQueue(null, data.accessToken);
        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        setAccessToken(null);
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

/** Télécharge un fichier protégé (le jeton d'accès est ajouté par l'intercepteur) et l'enregistre localement. */
export const downloadFile = async (url: string, fallbackName: string) => {
  const response = await api.get(url, { responseType: 'blob' });
  const disposition: string = response.headers['content-disposition'] || '';
  const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1] || match[2]) : fallbackName;

  const objectUrl = window.URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = objectUrl;
  link.setAttribute('download', fileName);
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(objectUrl);
};

/** Dépose un fichier en multipart/form-data (champ « file »). */
export const uploadFile = (url: string, file: File) => {
  const form = new FormData();
  form.append('file', file);
  return api.post(url, form, { headers: { 'Content-Type': 'multipart/form-data' } });
};
