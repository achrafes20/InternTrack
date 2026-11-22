import { expect, test } from '@playwright/test';
import { ACCOUNTS, Api, createStudent, firstCompanyId, isoDate, loginThroughUi } from './helpers';

/**
 * Parcours complet d'un stage, de la proposition à la soutenance planifiée :
 * proposition → soumission → validation → signature de la convention (3 parties) → démarrage
 * → dépôt du mémoire → planification de la soutenance → lien d'évaluation du tuteur.
 * Chaque étape passe par l'API réelle (mêmes appels que l'interface) ; l'interface est vérifiée
 * aux moments clés, du point de vue de l'étudiant.
 */
test('parcours complet d\'un stage PFE', async ({ page, request }) => {
  const admin = await Api.login(request, ACCOUNTS.admin.email, ACCOUNTS.admin.password);
  const studentAccount = await createStudent(admin);
  const student = await Api.login(request, studentAccount.email, studentAccount.password);
  const head = await Api.login(request, ACCOUNTS.head.email, ACCOUNTS.head.password);

  // 1. Proposition (16 semaines) puis soumission par l'étudiant
  const start = new Date();
  const end = new Date(start.getTime() + 16 * 7 * 24 * 3600 * 1000);
  const title = `PFE E2E ${Date.now()}`;
  const proposed = await student.post('/internships', {
    title,
    subjectDescription: 'Sujet créé par le test de bout en bout.',
    startDate: isoDate(start),
    endDate: isoDate(end),
    companyId: await firstCompanyId(student),
    academicTrackId: studentAccount.trackId,
    companySupervisorName: 'Tuteur E2E',
    companySupervisorEmail: 'tuteur.e2e@entreprise.ma',
  });
  const internshipId: string = proposed.id;
  await student.post(`/internships/${internshipId}/submit`);

  // 2. L'étudiant voit son dossier « Proposé » dans l'interface
  await loginThroughUi(page, studentAccount.email, studentAccount.password);
  await page.goto('/internships');
  await expect(page.getByText(title).first()).toBeVisible();

  // 3. Validation par le responsable de filière (crée la convention)
  await head.post(`/internships/${internshipId}/approve`);

  // 4. Signatures : étudiant, école, entreprise (signature papier : le scan signé est déposé comme preuve)
  await student.post(`/conventions/internship/${internshipId}/sign`, { party: 'Student' });
  await head.post(`/conventions/internship/${internshipId}/sign`, { party: 'School' });
  const scan = await head.upload(`/internships/${internshipId}/documents/signed-convention`, 'convention-signee.pdf',
    Buffer.from('%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n'));
  expect(scan.ok(), await scan.text()).toBeTruthy();
  await head.post(`/conventions/internship/${internshipId}/company-signature`, { signerName: 'Directeur E2E' });

  // 5. Démarrage du stage
  await head.post(`/internships/${internshipId}/start`);
  const running = await head.get(`/internships/${internshipId}`);
  expect(running.statusName ?? running.status).toBe('EnCours');

  // 6. Dépôt du mémoire (PDF) par l'étudiant, puis contrôle : un PDF contenant du JavaScript est refusé
  const cleanPdf = Buffer.from('%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n');
  const upload = await student.upload(`/internships/${internshipId}/documents/thesis`, 'memoire.pdf', cleanPdf);
  expect(upload.ok(), await upload.text()).toBeTruthy();

  const maliciousPdf = Buffer.from('%PDF-1.7\n1 0 obj\n<< /OpenAction << /S /JavaScript /JS (app.alert(1)) >> >>\nendobj\n%%EOF\n');
  const rejected = await student.upload(`/internships/${internshipId}/documents/thesis`, 'piege.pdf', maliciousPdf);
  expect(rejected.status()).toBe(400);
  expect(await rejected.text()).toMatch(/JavaScript/);

  // 7. Planification de la soutenance (jury : président, examinateur, tuteur entreprise)
  const teachers: any[] = await head.get(`/teachers?trackId=${studentAccount.trackId}`);
  const president = teachers.find((t) => t.email === ACCOUNTS.teacher.email) ?? teachers[0];
  const examiner = teachers.find((t) => t.id !== president.id);
  expect(examiner, 'deux enseignants disponibles dans la filière').toBeDefined();

  const defenseAt = new Date(end.getTime() - 7 * 24 * 3600 * 1000);
  defenseAt.setUTCHours(10, 0, 0, 0);
  const defense = await head.post('/defenses', {
    internshipId,
    scheduledAt: defenseAt.toISOString(),
    durationMinutes: 45,
    classroom: `Salle E2E ${Date.now()}`,
    juryMembers: [
      { teacherId: president.id, role: 'President' },
      { teacherId: examiner.id, role: 'Examinateur' },
      { teacherFullName: 'Tuteur E2E', role: 'TuteurEntreprise' },
    ],
  });

  // 8. Lien d'évaluation envoyé au tuteur (passe par la file d'e-mails)
  await head.post(`/defenses/${defense.id}/tutor-evaluation-link`);
  const outbox = await admin.get<any[]>('/email-outbox?take=20');
  expect(outbox.some((m) => m.toEmail === 'tuteur.e2e@entreprise.ma')).toBeTruthy();

  // 9. L'étudiant voit sa soutenance planifiée
  await page.goto('/defenses');
  await expect(page.getByText(title).first()).toBeVisible();

  // 10. Report (absence) : le dossier revient « en cours », puis la soutenance est replanifiée sur le même créneau
  await head.post(`/defenses/${defense.id}/cancel`, { reason: "Absence justifiée de l'élève" });
  expect((await head.get(`/internships/${internshipId}`)).statusName).toBe('EnCours');
  const rescheduled = await head.post('/defenses', {
    internshipId,
    scheduledAt: defenseAt.toISOString(),
    durationMinutes: 45,
    classroom: defense.classroom,
    juryMembers: [
      { teacherId: president.id, role: 'President' },
      { teacherId: examiner.id, role: 'Examinateur' },
    ],
  });
  expect(rescheduled.cancelledAt ?? null).toBeNull();
  expect((await head.get(`/internships/${internshipId}`)).statusName).toBe('SoutenancePlanifiee');
});

