using Microsoft.EntityFrameworkCore;
using StageFlow.Domain.Entities;

namespace StageFlow.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AcademicTrack> AcademicTracks { get; }
    DbSet<Company> Companies { get; }
    DbSet<InternshipOffer> InternshipOffers { get; }
    DbSet<Internship> Internships { get; }
    DbSet<Convention> Conventions { get; }
    DbSet<ProgressReport> ProgressReports { get; }
    DbSet<ReportComment> ReportComments { get; }
    DbSet<Defense> Defenses { get; }
    DbSet<DefenseJuryMember> DefenseJuryMembers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Exécute une opération en exclusion mutuelle (verrou partagé par toutes les instances de l'API),
    /// dans une transaction. À utiliser pour les opérations « vérifier puis écrire » sujettes aux courses,
    /// comme la planification de soutenances.
    /// </summary>
    Task<T> RunExclusiveAsync<T>(string lockName, Func<Task<T>> action, CancellationToken cancellationToken);
}

public static class LockNames
{
    public const string DefenseScheduling = "stageflow:defense-scheduling";
}

