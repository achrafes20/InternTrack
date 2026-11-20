export type UserRole = 'Etudiant' | 'Encadrant' | 'ResponsableFiliere' | 'Admin';

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  role: UserRole;
  studentNumber?: string;
  academicTrackId?: string;
  isActive?: boolean;
  academicTrackCode?: string;
  isLockedOut?: boolean;
}

export type UserDto = User;

export interface Track {
  id: string;
  code: string;
  name: string;
  description?: string;
}

export interface CompanyRef {
  id: string;
  name: string;
  city: string;
  industry?: string;
  address?: string;
  contactEmail?: string;
  contactPhone?: string;
  website?: string;
}

export interface Teacher {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  academicTrackId?: string;
}

export interface InternshipOffer {
  id: string;
  title: string;
  description: string;
  requiredSkills: string;
  city: string;
  durationInWeeks: number;
  monthlyStipend?: number;
  isActive: boolean;
  expirationDate: string;
  companyId: string;
  companyName: string;
  academicTrackId: string;
  academicTrackCode: string;
}

export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export type InternshipStatus =
  | 'Brouillon'
  | 'Propose'
  | 'Valide'
  | 'Refuse'
  | 'EnCours'
  | 'SoutenancePlanifiee'
  | 'Termine';

export interface ConventionStatus {
  conventionNumber: string;
  isSignedByStudent: boolean;
  isSignedByCompany: boolean;
  isSignedBySchool: boolean;
  signedAt?: string;
}

export type ConventionParty = 'Student' | 'Company' | 'School';

export interface Internship {
  id: string;
  title: string;
  subjectDescription: string;
  startDate: string;
  endDate: string;
  status: InternshipStatus;
  statusName: InternshipStatus;
  rejectionReason?: string;
  studentId: string;
  studentName?: string;
  academicSupervisorId?: string;
  academicSupervisorName?: string;
  companySupervisorName: string;
  companySupervisorEmail: string;
  companySupervisorPhone?: string;
  companyId: string;
  companyName: string;
  academicTrackId: string;
  academicTrackCode: string;
  internshipOfferId?: string;
  convention?: ConventionStatus;
}

export type InternshipDto = Internship;

export type ReportStatus = 'ARelire' | 'Valide';

export interface ReportComment {
  id: string;
  content: string;
  authorId: string;
  authorFullName: string;
  createdAt: string;
}

export interface ProgressReport {
  id: string;
  internshipId: string;
  weekNumber: number;
  title: string;
  activitiesPerformed: string;
  skillsAcquired: string;
  difficultiesEncountered: string;
  nextWeekGoals: string;
  aiSummary?: string;
  status: ReportStatus;
  createdAt: string;
  comments: ReportComment[];
  internshipTitle?: string;
  studentName?: string;
}

export type ProgressReportDto = ProgressReport;

export type DefenseRole = 'President' | 'Rapporteur' | 'Examinateur' | 'TuteurEntreprise';

export const DEFENSE_ROLE_LABELS: Record<DefenseRole, string> = {
  President: 'Président',
  Rapporteur: 'Rapporteur',
  Examinateur: 'Examinateur',
  TuteurEntreprise: 'Tuteur entreprise',
};

export interface JuryMember {
  id: string;
  teacherId: string;
  teacherFullName: string;
  role: DefenseRole;
  roleName: string;
}

export interface Defense {
  id: string;
  internshipId: string;
  studentId: string;
  studentName: string;
  internshipTitle: string;
  scheduledAt: string;
  durationMinutes: number;
  classroom: string;
  finalGrade?: number;
  comments?: string;
  juryMembers: JuryMember[];
}

export type DefenseDto = Defense;

export interface DefenseConflictCheckResult {
  hasConflict: boolean;
  conflictMessages: string[];
}

export interface DashboardStats {
  totalInternships: number;
  pendingValidation: number;
  inProgress: number;
  completed: number;
  statusDistribution: Array<{ status: string; count: number }>;
  topCompanies: Array<{ companyName: string; count: number }>;
  internshipsByTrack: Array<{ trackCode: string; trackName: string; count: number }>;
  upcomingDefenses: Array<{ id: string; studentName: string; internshipTitle: string; scheduledAt: string; classroom: string }>;
}

export interface AuditLog {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  userId?: string;
  userEmail?: string;
  timestamp: string;
  changes?: string;
}

export type AuditLogDto = AuditLog;

/** Extrait le message d'erreur d'une réponse ProblemDetails de l'API. */
export const apiErrorMessage = (err: any, fallback: string): string =>
  err?.response?.data?.detail || err?.response?.data?.title || err?.message || fallback;
