export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  role: 'Etudiant' | 'Encadrant' | 'ResponsableFiliere' | 'Admin';
  studentNumber?: string;
  academicTrackId?: string;
}

export type UserDto = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  academicTrackCode?: string;
};

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

export type InternshipStatus =
  | 'Brouillon'
  | 'Propose'
  | 'Valide'
  | 'Refuse'
  | 'EnCours'
  | 'SoutenancePlanifiee'
  | 'Termine';

export interface Internship {
  id: string;
  title: string;
  subjectDescription: string;
  startDate: string;
  endDate: string;
  status: InternshipStatus;
  statusName: string;
  rejectionReason?: string;
  studentId: string;
  academicSupervisorId?: string;
  companySupervisorName: string;
  companySupervisorEmail: string;
  companySupervisorPhone?: string;
  companyId: string;
  companyName: string;
  academicTrackId: string;
  academicTrackCode: string;
  internshipOfferId?: string;
}

export type InternshipDto = Internship & {
  studentName?: string;
  subject?: string;
};

export interface ProgressReport {
  id: string;
  internshipId: string;
  weekNumber: number;
  title?: string;
  content?: string;
  submissionDate?: string;
  activitiesPerformed?: string;
  skillsAcquired?: string;
  tasksCompleted?: string;
  tasksPlanned?: string;
  difficultiesEncountered?: string;
  nextWeekGoals?: string;
  aiSummary?: string;
  status: string;
  createdAt?: string;
  comments: ReportComment[];
}

export type ProgressReportDto = ProgressReport;

export interface ReportComment {
  id: string;
  content: string;
  authorId?: string;
  authorFullName?: string;
  createdAt: string;
}

export interface Defense {
  id: string;
  internshipId: string;
  studentName: string;
  internshipTitle?: string;
  internshipSubject?: string;
  defenseDate: string;
  scheduledAt?: string;
  durationMinutes?: number;
  classroom: string;
  grade?: number;
  finalGrade?: number;
  comments?: string;
  juryMembers: JuryMember[];
}

export type DefenseDto = Defense;

export interface DefenseConflictCheckResult {
  hasConflict: boolean;
  conflictMessages: string[];
}

export interface JuryMember {
  id?: string;
  teacherId?: string;
  teacherName?: string;
  teacherFullName?: string;
  role: string | number;
  roleName?: string;
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
