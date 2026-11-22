import { expect, type APIRequestContext, type Page } from '@playwright/test';

export const ACCOUNTS = {
  admin: { email: 'admin@ensa.ma', password: 'Admin@123456' },
  head: { email: 'responsable.gi@ensa.ma', password: 'Resp@123456' },
  teacher: { email: 'prof.tazi@ensa.ma', password: 'Prof@123456' },
} as const;

/** Client API authentifié (jeton Bearer), pour préparer les données sans dépendre de l'interface. */
export class Api {
  private constructor(private readonly request: APIRequestContext, private readonly token: string) {}

  static async login(request: APIRequestContext, email: string, password: string): Promise<Api> {
    const response = await request.post('/api/v1/auth/login', { data: { email, password } });
    expect(response.status(), `connexion de ${email}`).toBe(200);
    const body = await response.json();
    return new Api(request, body.accessToken);
  }

  private headers() {
    return { Authorization: `Bearer ${this.token}` };
  }

  async get<T = any>(url: string): Promise<T> {
    const response = await this.request.get(`/api/v1${url}`, { headers: this.headers() });
    expect(response.ok(), `GET ${url} → ${response.status()} ${await response.text()}`).toBeTruthy();
    return response.json();
  }

  /** Dépôt d'un fichier (multipart, champ « file ») ; renvoie la réponse brute pour tester les refus. */
  upload(url: string, fileName: string, buffer: Buffer) {
    return this.request.post(`/api/v1${url}`, {
      headers: this.headers(),
      multipart: { file: { name: fileName, mimeType: 'application/pdf', buffer } },
    });
  }

  async post<T = any>(url: string, data?: unknown): Promise<T> {
    const response = await this.request.post(`/api/v1${url}`, { headers: this.headers(), data: data ?? {} });
    expect(response.ok(), `POST ${url} → ${response.status()} ${await response.text()}`).toBeTruthy();
    const text = await response.text();
    return (text ? JSON.parse(text) : undefined) as T;
  }
}

const items = (value: any): any[] => (Array.isArray(value) ? value : value.items);

export const isoDate = (date: Date) => date.toISOString().substring(0, 10);

/** Crée un étudiant neuf de la filière GI (aucun dossier engagé) et renvoie ses identifiants. */
export async function createStudent(admin: Api) {
  const tracks = items(await admin.get<any>('/tracks'));
  const gi = tracks.find((t: any) => t.code === 'GI') ?? tracks[0];
  const suffix = `${Date.now()}${Math.floor(Math.random() * 1000)}`;
  const email = `e2e.${suffix}@ensa.ma`;
  const password = 'E2e@Test2026!';
  await admin.post('/users', {
    email, password, firstName: 'E2E', lastName: `Etudiant${suffix.slice(-4)}`,
    role: 'Etudiant', studentNumber: `E2E${suffix.slice(-8)}`, academicTrackId: gi.id,
    graduationYear: new Date().getFullYear() + 1,
  });
  return { email, password, trackId: gi.id as string };
}

export async function firstCompanyId(api: Api): Promise<string> {
  const companies = items(await api.get<any>('/companies'));
  expect(companies.length, 'au moins une entreprise dans le jeu de données').toBeGreaterThan(0);
  return companies[0].id;
}

/** Connexion par l'interface (formulaire de la page /login). */
export async function loginThroughUi(page: Page, email: string, password: string) {
  await page.goto('/login');
  await page.locator('input[type="email"]').fill(email);
  await page.locator('input[type="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(/\/dashboard/);
}
